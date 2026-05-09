namespace AstroPlannerWeb.Models;

public class ImagingSetup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Setup";

    public string TelescopeName { get; set; } = "";
    public double ApertureMm { get; set; }
    public double FocalLengthMm { get; set; }

    public string CameraName { get; set; } = "";
    public double PixelSizeMicrons { get; set; }
    public int SensorWidthPixels { get; set; }
    public int SensorHeightPixels { get; set; }

    public double QePercent { get; set; } = 65;
    public double ReadNoiseElectrons { get; set; } = 3.0;
    public double SubExposureSeconds { get; set; } = 300;
}
