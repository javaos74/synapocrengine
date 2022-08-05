using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using UiPath.OCR.Contracts;
using UiPath.OCR.Contracts.Activities;
using UiPath.OCR.Contracts.DataContracts;

namespace SynapOCRActivities.Basic.OCR
{
    public enum BoxesType
    {
        RAW = 1,
        BLOCK = 2,
        LINE =4 
    }
    public enum RequestType
    {
        upload,
        page
    }

    [DisplayName("Synap OCR Engine")]
    public class SynapOCREngine : OCRCodeActivity
    {
        [Category("Input")]
        [Browsable(true)]
        [Description("OCR 처리 할 대상 이미지")]
        public override InArgument<Image> Image { get => base.Image; set => base.Image = value; }

        [Category("Login")]
        [RequiredArgument]
        [Description("Synap OCR Engine 서버 정보 (예, http://호스트이름:포트번호 형식 마지막에 /sdk/ocr 포함 안됨)")]
        public InArgument<string> Endpoint { get; set; }

        [Category("Login")]
        [RequiredArgument]
        [Description("Synap OCR Engine 에서 제공하는 API KEY")]
        public InArgument<string> ApiKey { get; set; }

        [Category("Option")]
        [RequiredArgument]
        [Description("입력 파일 유형 upload/page 중 하나")]
        //public InArgument<string> Type { get; set; } = "upload";
        public RequestType Type { get; set;} = RequestType.upload;

        [Category("Option")]
        [Description("인식된 글자에 박스를 구성하는 옵션 기본값은 RAW")]
        public BoxesType BoxType { get; set; } = BoxesType.BLOCK;

        [Category("Option")]
        [Browsable(true)]
        [Description("인식할 언어 정보 사용가능한 언어는 kor, eng, num, sym, chn 과 all 이며 + 이용해서 조합할 수 있음")]
        public override InArgument<string> Language { get; set; } = "all";

        [Category("Option")]
        [Description("매스킹 이미지 저장 여부 (기본값 False)")]
        public bool SaveMask { get; set; } = false;

        [Category("Option")]
        [Description("매스킹 타입 ( #RRGGBBAA_{range} 혹은 mosaic_{range} 포맷)")]
        public InArgument<string> MaskType { get; set; } = "#FF000088_full";

        [Category("Option")]
        [Description("매스킹 사용시 출력 포맷")]
        public InArgument<string> MaskOutputFormat { get; set; } = "32BIT_RGBA";


        [Category("Option")]
        [Description("서식 인식 여부 (기본값은 False)")]
        public bool RecognizeForm { get; set; } = false;

        [Category("Option")]
        [Description("서식 식별자, 여러개 사용시 + 로 연결해서 지정")]
        public InArgument<string> FormIdList { get; set; }

        [Category("Option")]
        [Description("이전 요청의 응답에 있는 fid 값")]
        public InArgument<string> Fid { get; set; }

        [Category("Option")]
        [Description("요청 종류가 page인 경우 요청하는 페이지 인덱스")]
        public InArgument<int> PageIndex { get; set; } = 0;

        [Category("Option")]
        [Description("찾고자 하는 문자의 정규식 (Python 정규식 표현)")]
        public InArgument<string> Pattern { get; set; }


        [Category("Output")]
        [Browsable(true)]
        [Description("인식된 전체 텍스트")]
        public override OutArgument<string> Text { get => base.Text; set => base.Text = value; }

        [Category("Output")]
        [Description("매스킹된 이미지 파일 경로")]
        public OutArgument<string> MaskedFilePath { get; set; }

        [Category("Output")]
        [Description("인식된 폼 데이터가 저장된 CSV 파일 경로 ")]
        public OutArgument<string> CsvFormFilePath { get; set; }

        [Category("Output")]
        [Description("Synap OCR Engine 전체 JSON 응답 데이터")]
        public OutArgument<string> JsonResponse { get; set; }

        [Category("Output")]
        [Description("Synap OCR Matched Pattern 결과 (SynapOCRField [] 타입)")]
        public OutArgument<SynapOCRField []> MatchedBoxes { get; set; }

        [Category("Output")]
        [Description("인식된 폼 결과 JSON 데이터")]
        [Browsable(false)]
        public OutArgument<string> FormResponse { get; set; }

        [Category("Output")]
        [Description("인식된 폼 결과 데이터 (SynapForm [] 타입)")]
        public OutArgument<SynapForm[]> MatchedForms { get; set; }

        [Category("Output")]
        [Description("멀티페이지 문서의 경우 전체 페이지 수")]
        public OutArgument<int> TotalPage { get; set; }

        [Category("Output")]
        [Description("현재 페이지")]
        public OutArgument<int> CurrentPage { get; set; }
 
        [Category("Output")]
        [Description("Synap OCR Engine 응답 성공/실패 코드 및 설명")]
        public OutArgument<string> Status { get; set; }


        private string filepath;

        private string masked_file_path;
        private string csv_file_path;

        private string form_response;
        private SynapForm[] matched_forms; 
        private string json_response;
        private SynapOCRField[]  matched_boxes = null; 
        private string status;
        private int total_page;

        /**
         * OCRENgine으로 동작하는데 필요한 함수 구현 
         * Dictionary<string,object> options에 필요한 값을 담아서 넘겨준다. 
         */
        public override Task<OCRResult> PerformOCRAsync(Image image, Dictionary<string, object> options, CancellationToken ct)
        {
            //filepath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            filepath = System.IO.Path.GetTempFileName();
            if ( image != null ) {
                if (System.IO.File.Exists(filepath))
                    System.IO.File.Delete(filepath);
#if DEBUG
                System.Console.WriteLine($"width={image.Width}, height={image.Height} resolution={image.HorizontalResolution} ");
#endif
                image.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
            } 
            else
            {
                filepath = string.Empty;
            }
 #if DEBUG
            System.Console.WriteLine("temp file path " + filepath);
#endif

            var result =  OCRResultHelper.FromSynapClient(filepath, options);
            masked_file_path = options.ContainsKey("masked_image") ? options["masked_image"].ToString() : "";
            csv_file_path = options.ContainsKey("csv_file_name") ? options["csv_file_name"].ToString() : "";
            form_response = options.ContainsKey("matched_forms") ? options["matched_forms"].ToString() : "";
            json_response = options.ContainsKey("json_response") ? options["json_response"].ToString() : "";
            status = options.ContainsKey("status") ? options["status"].ToString() : "200: OK";
            total_page = options.ContainsKey("total_page") ? (int)options["total_page"] : 1;
            if( options.ContainsKey("matched_boxes"))
                matched_boxes = (SynapOCRField[]) options["matched_boxes"];
            if (options.ContainsKey("matched_forms"))
                matched_forms = (SynapForm[])options["matched_forms"];

#if DEBUG
            System.Console.WriteLine("masked file path : " + masked_file_path);
#endif
            if (System.IO.File.Exists(filepath))
                System.IO.File.Delete(filepath);

            return result;
        }

        /**
         * Output 출력을 설정한다. PeformOCRAsync에서 options에 담겨진 값을 이용해서 최종 Output argument에 값을 설정한다. 
         */
        protected override void OnSuccess(CodeActivityContext context, OCRResult result)
        {

            if( !string.IsNullOrEmpty(masked_file_path))
                MaskedFilePath.Set(context, masked_file_path);

            if( !string.IsNullOrEmpty(csv_file_path))
                CsvFormFilePath.Set(context, csv_file_path);

            if( !string.IsNullOrEmpty( form_response))
                FormResponse.Set(context, form_response);

            if( !string.IsNullOrEmpty( json_response))
                JsonResponse.Set(context, json_response);

            if (matched_boxes != null)
                MatchedBoxes.Set(context, matched_boxes);
            else
                MatchedBoxes.Set(context, new SynapOCRField[] { });

            if( !string.IsNullOrEmpty( status))
                Status.Set(context, status);

            if (matched_forms != null)
                MatchedForms.Set(context, matched_forms);
            else
                MatchedForms.Set(context, new SynapForm[] { });

            TotalPage.Set(context, total_page);
        }

        protected override Dictionary<string, object> BeforeExecute(CodeActivityContext context)
        {
            return new Dictionary<string, object>
            {
                { "endpoint",  Endpoint.Get(context) },
                { "apikey", ApiKey.Get(context) },
                { "type", Type },
                { "fid", Fid.Get(context) != null ? Fid.Get(context) : string.Empty},
                { "pattern", Pattern.Get(context) != null ? Pattern.Get(context) : string.Empty},
                { "page_index", PageIndex.Get(context) },
                { "boxes_type", BoxType },
                { "langs", Language.Get(context) },
                { "save_mask", SaveMask },
                { "mask_type", MaskType.Get(context) != null ? MaskType.Get(context) : string.Empty},
                { "output_format", MaskOutputFormat.Get(context) },
                { "recog_form", RecognizeForm },
                { "form_id_list", FormIdList.Get(context) != null ? FormIdList.Get(context) : string.Empty }
            };
        }
    }
}
