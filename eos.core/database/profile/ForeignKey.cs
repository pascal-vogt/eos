namespace eos.core.database
{
    public class ForeignKey
    {
        public string Name { get; set; }
        public bool Follow { get; set; }
        public bool GoToParent { get; set; }
    }
}