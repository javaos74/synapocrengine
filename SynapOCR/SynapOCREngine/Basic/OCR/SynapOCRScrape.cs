using System.Collections.Generic;
using UiPath.OCR.Contracts.Activities;
using UiPath.OCR.Contracts.Scrape;

namespace SynapOCRActivities.Basic.OCR
{
    // Extend OCRScrapeBase to allow your OCR engine to display custom user controls when integrating
    // with wizards such as Screen Scraping or Template Manager.
    internal class SynapOCRScrape : OCRScrapeBase
    {
        private readonly SynapScrapeControl _sampleScrapeControl;

        public override ScrapeEngineUsages Usage { get; } = ScrapeEngineUsages.Document | ScrapeEngineUsages.Screen;

        public SynapOCRScrape(IOCRActivity ocrEngineActivity, ScrapeEngineUsages usage) : base(ocrEngineActivity)
        {
            _sampleScrapeControl = new SynapScrapeControl(usage);
        }

        public override ScrapeControlBase GetScrapeControl()
        {
            return _sampleScrapeControl;
        }

        public override Dictionary<string, object> GetScrapeArguments()
        {
            return new Dictionary<string, object>
            {
                { "option1", _sampleScrapeControl.SampleInput }
            };
        }
    }
}
