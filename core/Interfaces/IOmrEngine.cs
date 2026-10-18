using core.Models;

namespace core.Interfaces
{
    public interface IOmrEngine
    {
        SheetOmrResult Process(Stream imageStream, FormLayout layout);
    }
}