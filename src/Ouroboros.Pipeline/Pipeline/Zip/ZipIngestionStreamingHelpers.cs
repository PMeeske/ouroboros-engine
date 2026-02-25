namespace Ouroboros.Pipeline.Ingestion.Zip;

internal static class ZipIngestionStreamingHelpers
{
    public static bool IsLikelyText(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return true;
        int control = 0;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (b == 9 || b == 10 || b == 13) continue;
            if (b >= 32 && b < 127) continue;
            control++;
            if (control > data.Length / 10) return false;
        }
        return true;
    }
}