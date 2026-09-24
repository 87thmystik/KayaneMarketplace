namespace Kayane.ViewModels;

public class BulkActionResultVM
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Skipped { get; set; }

    public string SuccessMessage()
    {
        var suffix = Skipped > 0 ? $" ({Skipped} skipped)" : "";
        return $"{Succeeded} of {Total} item{(Total == 1 ? "" : "s")} processed{suffix}.";
    }
}