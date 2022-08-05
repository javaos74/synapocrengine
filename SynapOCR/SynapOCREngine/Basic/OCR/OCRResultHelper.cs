using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UiPath.OCR.Contracts.DataContracts;
using UiPath.OCR.Contracts;

namespace SynapOCRActivities.Basic.OCR
{
    internal static class OCRResultHelper
    {

        internal static  UiPath.OCR.Contracts.OCRRotation GetOCRRotation( Single rot)
        {
 #if DEBUG
            System.Console.WriteLine(" roation : " + rot);
 #endif
            if ( rot >= 45 && rot < 90 + 45)
                return OCRRotation.Rotated90;
            else if ( rot >= 90 + 45 && rot < 180 + 45)
                return OCRRotation.Rotated180;
            else if( rot >= 180 + 45 && rot < 270 + 45)
                return OCRRotation.Rotated270;
            else if ( rot >= 270 + 45 || rot < 45)
                return OCRRotation.None;
            else
                return OCRRotation.Other;
        } 

        internal static async Task<OCRResult> FromSynapClient ( string filePath, Dictionary<string, object> options) 
        {
            OCRResult ocrResult = null;
            var client = new UiPathHttpClient( options["endpoint"].ToString());
            client.AddField("api_key", options["apikey"].ToString());
            client.AddField("langs", options["langs"].ToString());
            client.AddField("coord", "origin");
            client.AddField("skew", "image");
            client.AddField("textout", "true");
            //입력 요청이 upload인 경우 
            if ( (RequestType) options["type"] == RequestType.upload)
            {
                client.AddField("type", "upload");
                client.AddFile(filePath);
            }
            //입력 요청이 page 인 경우 
            else
            {
                client.AddField("type", "page");
                client.AddField("fid", options["fid"].ToString());
                client.AddField("page_index", options["page_index"].ToString());
            }
 
            if( !string.IsNullOrEmpty( options["pattern"].ToString()))
            {
                client.AddField("pattern", options["pattern"].ToString());
            }
            //매스킹 파일 저장 시 추가 옵션 
            if( (bool) options["save_mask"]) 
            { 
                client.AddField("save_mask", "true");
                client.AddField("mask_type", options["mask_type"].ToString());
                client.AddField("output_format", options["output_format"].ToString());
            }
            //폼 인식시 추가 옵션 
            if( (bool)options["recog_form"])
            {
                client.AddField("recog_form", "true");
                client.AddField("form_id_list", options["form_id_list"].ToString());
            }
            BoxesType boxType = (BoxesType)options["boxes_type"];
            if( boxType == BoxesType.RAW)
                client.AddField("boxes_type", "raw");
            else if ( boxType == BoxesType.BLOCK)
                client.AddField("boxes_type", "block");
            else
                client.AddField("boxes_type", "line");

            var resp = await client.Upload();
#if DEBUG
            System.Console.WriteLine(  resp.status + " == > " + (resp.body.Length > 100 ? resp.body.Substring(0, 100) : resp.body));
            System.IO.Directory.CreateDirectory(@"C:\Temp");
            System.IO.File.WriteAllText( @"C:\Temp\synap.json", resp.body);
#endif
            if ( resp.status == HttpStatusCode.OK)
            {
                SynapOCRResponse synapResult = JsonConvert.DeserializeObject<SynapOCRResponse>(resp.body, new SynapOCRResponseConverter());
                OCRRotation rotation = GetOCRRotation( 360 - Convert.ToSingle(synapResult.result.rotate));
                options.Add("json_response", resp.body); // 전체 JSON 응답 
                if( synapResult.result.matched_boxes.Count() > 0)
                    options.Add("matched_boxes", synapResult.result.GetFields( (int)SynapFieldType.MATCHED));
                // TO-BE-FIX 
                if( (bool)options["recog_form"]) 
                    options.Add("matched_forms", synapResult.result.GetSynapForms());
#if DEBUG
                Console.WriteLine("rotation enum : " + rotation.ToString());
#endif
                ocrResult = new OCRResult
                {
                    Text = synapResult.result.full_text,
                    Words = synapResult.result.GetFields((int)boxType).Select(word => new Word
                    {
                        Text = word.Text,
                        Confidence = Convert.ToInt32(100 * word.Confidence),
                        // charcter polygonpoints check 
                        Characters = word.Text.Select((ch, idx) => new Character
                        {
                            Char = ch,
                            Confidence = Convert.ToInt32(100 * word.Confidence),
                            Rotation = rotation,
                            PolygonPoints = reducePolygonPoints(word.Text, idx, word.Points.ToArray(), rotation)  // 회전에 대해서 고려해줘야 함 
                        }).ToArray(),
                        PolygonPoints = word.Points.ToArray()
                    }).ToArray(),
                    Confidence = 0,
                    //실제 어떤 값을 줘야 하는지 체크해봐야 함 
                    SkewAngle = 0 // rotation == OCRRotation.Other ? -1 * Convert.ToSingle(synapResult.result.rotate) : 0 // Convert.ToSingle(synapResult.result.rotate)
                };


                //파일을 가져와야 하는 경우라면 여기에서 파일을 가져오는 호출을 하고 결과를 결과를 out param을 전달해야 한다 
                if( !string.IsNullOrEmpty(synapResult.result.masked_image))
                {
                    client.Clear();
                    client.AddField("api_key", options["apikey"].ToString());
                    var targetFilePath = await client.GetResultFile(synapResult.result.masked_image);
                    options.Add("masked_image", targetFilePath);
#if DEBUG
                    Console.WriteLine("masked file path : " + targetFilePath);
#endif 
                }
                if( !string.IsNullOrEmpty(synapResult.result.csv_file_name))
                {
                    //options["matched_forms2"] = synapResult.result.GetSynapForms();
                    client.Clear();
                    client.AddField("api_key", options["apikey"].ToString());
                    var targetFilePath = await client.GetResultFile(synapResult.result.csv_file_name);
                    options.Add("csv_file_name", targetFilePath);
#if DEBUG
                    Console.WriteLine("csv file path : " + targetFilePath);
#endif 
                }
                options.Add("status", "200: OK");//성공인 경우 상태 값 
                return ocrResult;
            }
            else
            {
                var error = JsonConvert.DeserializeObject<Dictionary<string,object> >( resp.body);
                options.Add("status", $"{error["status"]} :  {error["result"]}" );
#if DEBUG
                Console.WriteLine($"오류 코드 : {error["status"]}, 오류 메세지: {error["result"]}");
#endif
                //throw new Exception($"Synap OCR Engine에서 오류 발생:\n 오류 코드: {error["status"]}\n 오류 메세지: {error["result"]}");
                return new OCRResult();
            }

        }

        internal static PointF[] reducePolygonPoints ( string word, int idx,  PointF [] points,  OCRRotation rot)
        {
            var x = points[0].X;
            var y = points[0].Y;
            var w = Math.Abs(points[1].X - x);
            var h = Math.Abs(points[1].Y - y);
            var y2 = points[3].Y;
            var x2 = points[3].X;

            float dx = w / word.Length;
            float dy = h / word.Length;

            if( rot == OCRRotation.Rotated90)
                return new[] { new PointF(x, y - dy * idx), new PointF(x, y - dy * (idx + 1)), new PointF(x2, y - dy * (idx + 1)), new PointF(x2, y - dy * idx) };
            else if ( rot == OCRRotation.Rotated270 )
                return new[] { new PointF(x, y + dy * idx), new PointF(x, y + dy * (idx + 1)), new PointF(x2, y + dy * (idx + 1)), new PointF(x2, y + dy * idx) };
            else if( rot == OCRRotation.Rotated180)
                return new[] { new PointF(x - dx * idx, y), new PointF(x - dx * (idx + 1), y), new PointF(x - dx * (idx + 1), y2), new PointF(x - dx * idx, y2) };
            else
                return new[] { new PointF(x + dx * idx, y), new PointF(x + dx * (idx + 1), y), new PointF(x + dx * (idx + 1), y2), new PointF(x + dx * idx, y2) };

        }

    }
}
