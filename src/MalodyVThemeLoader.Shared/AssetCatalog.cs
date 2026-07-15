using System.Collections.Generic;

namespace MalodyVThemeLoader.Shared;

public sealed class AssetCatalog
{
    public int FormatVersion { get; set; } = 1;
    public string UnityVersion { get; set; } = "";
    public List<ImageCatalogEntry> Images { get; set; } = new();
    public List<FileCatalogEntry> Live2dFiles { get; set; } = new();
}

public sealed class ImageCatalogEntry
{
    public string LogicalId { get; set; } = "";
    public string RuntimeName { get; set; } = "";
    public string TextureName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public Float2 Pivot { get; set; } = new(0.5f, 0.5f);
    public Float4 Border { get; set; } = new(0, 0, 0, 0);
    public float PixelsPerUnit { get; set; } = 100;
    public int MeshType { get; set; }
}

public sealed class FileCatalogEntry
{
    public string LogicalId { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class Float2
{
    public Float2()
    {
    }

    public Float2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class Float4
{
    public Float4()
    {
    }

    public Float4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }
}
