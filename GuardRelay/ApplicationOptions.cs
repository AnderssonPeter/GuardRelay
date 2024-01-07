using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuardRelay;
internal class ApplicationOptions
{
    public TimeSpan FetchInterval { get; set; } = TimeSpan.FromSeconds(30);
    public string DatabaseLocation { get; set; } = ".\\GuardRelay.sqlite";
}
