namespace eos.core.database
{
    public class ForeignKeyDescriptor
    {
        public KeyDescriptor Source { get; set; }
        public KeyDescriptor Target { get; set; }

        public override string ToString()
        {
            return $"{Source}->{Target}";
        }
    }
}