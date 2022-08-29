namespace RequestForRights.GLPI.Sync
{
    public class RequestForRightsRight
    {
        public int IdResource { get; set; }
        public string ResourceName { get; set; }
        public string RequestRightGrantType { get; set; }
        public string ResourceRightName { get; set; }
        public string ResourceRightDescription { get; set; }
    }
}