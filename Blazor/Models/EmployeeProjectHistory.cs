using CsvHelper.Configuration.Attributes;
using static System.Formats.Asn1.AsnWriter;
using System.Globalization;
using CsvHelper.Configuration;

namespace Blazor.Models
{
    public class EmployeeProjectHistory
    {
        [Name("EmpID")]
        public int EmpId { get; set; }

        [Name("ProjectID")]
        public int ProjectId { get; set; }

        [Name("DateFrom")]
        public DateTime DateFrom { get; set; }

        [Name("DateTo")]
        public DateTime? DateTo { get; set; }
    }

    public class EmployeeProjectHistoryMap : ClassMap<EmployeeProjectHistory>
    {
        public EmployeeProjectHistoryMap()
        {
            Map(m => m.DateFrom).TypeConverterOption.Format("M_d_yy");
            Map(m => m.DateTo).TypeConverterOption.Format("M_d_yy");
        }
    }
}
