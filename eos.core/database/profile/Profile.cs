namespace eos.core.database
{
    using System.Collections.Generic;

    public class Profile
    {
        public List<Table> Tables { get; set; }
        public List<ForeignKey> ForeignKeys { get; set; }
    }
}