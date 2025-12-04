namespace Kumparam.Core.Models;

public class ScrapingConfig
{
    public int ConfigId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string HtmlPath_Buying { get; set; } = string.Empty;
    public string HtmlPath_Selling { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Web";
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}