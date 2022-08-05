using UiPath.OCR.Contracts.Scrape;

namespace SynapOCRActivities.Basic.OCR
{
    public class SynapOCRScrapeFactory : OCRScrapeFactory
    {
        public override OCRScrapeBase CreateEngine(ScrapeEngineUsages usage)
        {
            return new SynapOCRScrape(new SynapOCREngine(), usage);
        }
    }
}
