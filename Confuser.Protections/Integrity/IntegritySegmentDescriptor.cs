namespace Confuser.Protections.Integrity
{
    internal sealed class IntegritySegmentDescriptor
    {
        public int Id;
        public string Kind;
        public string Name;
        public long Length;
        public byte[] Digest;
    }
}
