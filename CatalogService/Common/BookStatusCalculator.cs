namespace CatalogService.Common;

public static class BookStatusCalculator
{
    public static string Compute(int availableCopies) => availableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT";
}
