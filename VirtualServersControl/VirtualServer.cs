namespace VirtualServersControl
{
    public class VirtualServer
    {
        public int VirtualServerID { get; set; }
        public DateTime CreateDateTime { get; set; } = DateTime.Now;
        public DateTime? RemoveDateTime { get; set; }
        public bool? SelectedForRemove { get; set; }
    }
}
