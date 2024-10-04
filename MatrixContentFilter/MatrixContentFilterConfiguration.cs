using Microsoft.Extensions.Configuration;

namespace MatrixContentFilter;

public class MatrixContentFilterConfiguration {
    public MatrixContentFilterConfiguration(IConfiguration config) => config.GetRequiredSection("MatrixContentFilter").Bind(this);

    public List<string> Admins { get; set; } = new();
    public ConcurrencyLimitsConfiguration ConcurrencyLimits { get; set; } = new();
    
    public string AppMode { get; set; } = "bot";
    public string AsyncQueueImplementation { get; set; } = "lifo";
    
    public class ConcurrencyLimitsConfiguration {
        public int Redactions { get; set; } = 1;
        public int LogMessages { get; set; } = 1;
    }
}
