namespace CipherCapsule.Builder;
public enum OutputFormat { EXE, CCAP }
public static class ArchiveOutputSelection
{
    public static OutputFormat FromSelfExtracting(bool enabled)=>enabled?OutputFormat.EXE:OutputFormat.CCAP;
    public static bool IsSelfExtracting(OutputFormat format)=>format==OutputFormat.EXE;
}
