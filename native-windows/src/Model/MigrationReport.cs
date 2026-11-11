using System;
using System.Collections.Generic;

namespace Kerf.Model
{
    public enum OrphanKind
    {
        Track = 0,
        Listener = 1
    }

    public class OrphanedReferenceItem
    {
        public string Id { get; set; }
        public OrphanKind Kind { get; set; }
        public uint NumericId { get; set; }
        public string TargetLayerId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string SuggestedAction { get; set; } // "Keep", "Rebind", "Purge"

        public OrphanedReferenceItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Kind = OrphanKind.Track;
            NumericId = 0;
            TargetLayerId = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            IsActive = false;
            SuggestedAction = "Keep";
        }
    }

    public class MigrationReport
    {
        public string SourceVersion { get; set; }
        public string TargetVersion { get; set; }
        public int TotalLayersChecked { get; set; }
        public int ResolvedTracksCount { get; set; }
        public int UnresolvedTracksCount { get; set; }
        public int ResolvedListenersCount { get; set; }
        public int UnresolvedListenersCount { get; set; }
        public List<OrphanedReferenceItem> OrphanedItems { get; set; }
        public List<string> Warnings { get; set; }

        public bool RequiresUserReview
        {
            get
            {
                return UnresolvedTracksCount > 0 || UnresolvedListenersCount > 0;
            }
        }

        public MigrationReport()
        {
            SourceVersion = "3.0.0";
            TargetVersion = "4.0.0";
            TotalLayersChecked = 0;
            ResolvedTracksCount = 0;
            UnresolvedTracksCount = 0;
            ResolvedListenersCount = 0;
            UnresolvedListenersCount = 0;
            OrphanedItems = new List<OrphanedReferenceItem>();
            Warnings = new List<string>();
        }
    }
}
