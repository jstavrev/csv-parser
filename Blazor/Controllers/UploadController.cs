using Blazor.Models;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NLog;
using System.Formats.Asn1;
using System.Globalization;

namespace Blazor.Controllers
{
    public class UploadController : Controller
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly IHubContext<CsvParserHub> _parserHubContext;

        public UploadController(IHubContext<CsvParserHub> parserHubContext)
        {
            _parserHubContext = parserHubContext;
        }

        [HttpPost("upload/single")]
        public async Task<IActionResult> UploadSingle(IFormFile file)
        {
            try
            {
                //Validate the uploaded file is a csv one.
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".csv")
                {
                    await _parserHubContext.Clients.All.SendAsync("UploadError", "The file must be a CSV.");
                    return BadRequest();
                }

                //Get date format header
                var dateFormat = Request.Headers["Date-Format"];
                if (String.IsNullOrEmpty(dateFormat))
                {
                    await _parserHubContext.Clients.All.SendAsync("UploadError", "Date format header missing.");
                    return BadRequest();
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    NewLine = Environment.NewLine,
                };
                var options = new TypeConverterOptions { Formats = new[] { dateFormat.ToString() } };
                var test = options.NullValues;

                //Parse CSV input to an object collection.
                List<EmployeeProjectHistory> records = null;
                using (var reader = new StreamReader(file.OpenReadStream()))
                using (var csv = new CsvReader(reader, config))
                {
                    csv.Context.TypeConverterOptionsCache.AddOptions<DateTime>(options);
                    csv.Context.TypeConverterOptionsCache.AddOptions<DateTime?>(options);
                    csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().NullValues.Add("NULL");
                    records = csv.GetRecords<EmployeeProjectHistory>().ToList();
                }

                //can an employee work on the same project in 2 different periods (jan 2020-feb 2020, july 2020-dec 2020)?
                //is the csv input only of a pair of employees and all their project history or input has more than 2 employees and we looking for longest time on common projects?
                //should we be including start/end dates? can change result by a day

                //Group employees by project.
                var projectRecords = records.GroupBy(e => e.ProjectId);

                //Dictionary with tuple (employeOneId, employeTwoId, projectId) as key to avoid unneeded list iterrations
                var overlapsDict = new Dictionary<(int, int, int), EmployeesProjectOverlap>();

                //Go over every pair and find common projects and overlap duration
                foreach (var project in projectRecords)
                {
                    var employees = project.ToList();

                    for (int i = 0; i < employees.Count; i++)
                    {
                        for (int j = i + 1; j < employees.Count; j++)
                        {
                            var overlapDays = CalculateOverlap(employees[i], employees[j]);
                            if (overlapDays != 0)
                            {
                                //Standardize the complex key (id 3, id 5 == id 5, id 3) to avoid double entries
                                int empOneId = Math.Min(employees[i].EmpId, employees[j].EmpId);
                                int empTwoId = Math.Max(employees[i].EmpId, employees[j].EmpId);

                                var key = (empOneId, empTwoId, project.Key);

                                //Check if there is already an entry for this employees overlapping on this project and if there is sum the days.
                                if (overlapsDict.TryGetValue(key, out var existingOverlap))
                                {
                                    existingOverlap.DaysWorkedTogether += overlapDays;
                                }
                                else
                                {
                                    overlapsDict[key] = new EmployeesProjectOverlap
                                    {
                                        EmployeeOneId = empOneId,
                                        EmployeeTwoId = empTwoId,
                                        ProjectId = project.Key,
                                        DaysWorkedTogether = overlapDays
                                    };
                                }
                            }
                        }
                    }
                }

                await _parserHubContext.Clients.All.SendAsync("ReceiveUpload", overlapsDict.Values.ToList());
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                await _parserHubContext.Clients.All.SendAsync("UploadError", "Server error.");
                logger.Error($"{DateTime.UtcNow} | UploadSingle failed: {ex}. Message: {ex.Message}");
                return StatusCode(500, ex.Message);
            }
        }

        private int CalculateOverlap(EmployeeProjectHistory a, EmployeeProjectHistory b)
        {
            DateTime currentDate = DateTime.Today;

            var aEndDate = a.DateTo ?? currentDate;
            var bEndDate = b.DateTo ?? currentDate;

            DateTime latestStart = a.DateFrom > b.DateFrom ? a.DateFrom : b.DateFrom;
            DateTime earliestEnd = aEndDate < bEndDate ? aEndDate : bEndDate;

            if (latestStart <= earliestEnd)
            {
                //adding 1 day to include the end date?
                return (int)(earliestEnd - latestStart).TotalDays + 1;
            }

            return 0;
        }
    }
}
