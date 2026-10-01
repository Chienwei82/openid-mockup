public enum JwtTokenSegment
{
    Header,
    Payload,
    Signature
}

public static class JwtTokenSegmentLimits
{
    public const int ExpectedPartCount = 3;
}