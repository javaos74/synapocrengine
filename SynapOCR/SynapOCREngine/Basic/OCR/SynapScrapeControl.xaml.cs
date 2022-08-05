using UiPath.OCR.Contracts.Scrape;

namespace SynapOCRActivities.Basic.OCR
{
    /// <summary>
    /// Interaction logic for SimpleScrapeControl.xaml
    /// </summary>
    internal partial class SynapScrapeControl : ScrapeControlBase
    {
        public string SampleInput { get; set; }

        public SynapScrapeControl()
            : this (ScrapeEngineUsages.Screen)
        {
        }

        public SynapScrapeControl(ScrapeEngineUsages usage)
        {
            Usage = usage;
            InitializeComponent();
            DataContext = this;
        }
    }
}
