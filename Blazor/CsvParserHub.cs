using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Blazor
{
    public class CsvParserHub : Hub
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    }
}
