namespace AstroPlannerWeb.Models;

[Flags]
public enum CatalogSource
{
    None     = 0,
    NGC      = 1 << 0,
    IC       = 1 << 1,
    Messier  = 1 << 2,
    Caldwell = 1 << 3,
}
