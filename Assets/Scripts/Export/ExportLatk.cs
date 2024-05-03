// Copyright 2021 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SimpleJSON;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using static TiltBrush.ExportUtils;

namespace TiltBrush
{

    public static class ExportLatk
    {

        // Latk has the ability to store multiple layers and frames, but we're not using that here
        private static int layerNum = 1;
        private static int frameNum = 1;

        public static void Export(string writeFileName)
        {
            string ext = Path.GetExtension(writeFileName).ToLower();
            Debug.Log("Found extension " + ext);
            bool useZip = (ext == ".latk" || ext == ".zip");

            List<string> FINAL_LAYER_LIST = new List<string>();

            for (int hh = 0; hh < layerNum; hh++)
            {
                int currentLayer = hh;

                List<string> sb = new List<string>();
                List<string> sbHeader = new List<string>();
                sbHeader.Add("\t\t\t\t\t\"frames\":[");
                sb.Add(string.Join("\n", sbHeader.ToArray()));

                for (int h = 0; h < frameNum; h++)
                {
                    int currentFrame = h;

                    List<string> sbbHeader = new List<string>();
                    sbbHeader.Add("\t\t\t\t\t\t{");
                    sbbHeader.Add("\t\t\t\t\t\t\t\"strokes\":[");
                    sb.Add(string.Join("\n", sbbHeader.ToArray()));

                    LinkedListNode<Stroke> node = SketchMemoryScript.m_Instance.GetMemoryList.First;
                    if (node == null) continue;
                    Stroke stroke = node.Value;

                    for (int i = 0; i < SketchMemoryScript.m_Instance.StrokeCount; i++)
                    {
                        // Simpler way to access strokes in the linked list by index, but possibly more expensive
                        //Stroke stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(i);

                        if (i > 0)
                        {
                            node = node.Next;
                            stroke = node.Value;
                        }

                        List<string> sbb = new List<string>();
                        sbb.Add("\t\t\t\t\t\t\t\t{");
                        float r = stroke.m_Color.r;
                        float g = stroke.m_Color.g;
                        float b = stroke.m_Color.b;
                        sbb.Add("\t\t\t\t\t\t\t\t\t\"color\":[" + r + ", " + g + ", " + b + "],");

                        if (stroke.IsGeometryEnabled && stroke.m_ControlPoints.Length > 0)
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t\t\"points\":[");
                            for (int j = 0; j < stroke.m_ControlPoints.Length; j++)
                            {
                                PointerManager.ControlPoint point = stroke.m_ControlPoints[j];
                                float x = point.m_Pos.x;
                                float y = point.m_Pos.y;
                                float z = point.m_Pos.z;

                                float pressureVal = point.m_Pressure;

                                if (j == stroke.m_ControlPoints.Length - 1)
                                {
                                    sbb.Add("\t\t\t\t\t\t\t\t\t\t{\"co\":[" + x + ", " + y + ", " + z + "], \"pressure\":" + pressureVal + ", \"strength\":1}");
                                    sbb.Add("\t\t\t\t\t\t\t\t\t]");
                                }
                                else
                                {
                                    sbb.Add("\t\t\t\t\t\t\t\t\t\t{\"co\":[" + x + ", " + y + ", " + z + "], \"pressure\":" + pressureVal + ", \"strength\":1},");
                                }
                            }
                        }
                        else
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t\t\"points\":[]");
                        }

                        if (i == SketchMemoryScript.m_Instance.StrokeCount - 1)
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t}");
                        }
                        else
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t},");
                        }

                        sb.Add(string.Join("\n", sbb.ToArray()));
                    }

                    List<string> sbFooter = new List<string>();
                    if (h == frameNum - 1)
                    {
                        sbFooter.Add("\t\t\t\t\t\t\t]");
                        sbFooter.Add("\t\t\t\t\t\t}");
                    }
                    else
                    {
                        sbFooter.Add("\t\t\t\t\t\t\t]");
                        sbFooter.Add("\t\t\t\t\t\t},");
                    }
                    sb.Add(string.Join("\n", sbFooter.ToArray()));
                }

                FINAL_LAYER_LIST.Add(string.Join("\n", sb.ToArray()));
            }

            Debug.Log("+++ Parsing finished. Begin file writing.");

            List<string> s = new List<string>();
            s.Add("{");
            s.Add("\t\"creator\": \"unity\",");
            s.Add("\t\"grease_pencil\":[");
            s.Add("\t\t{");
            s.Add("\t\t\t\"layers\":[");

            for (int i = 0; i < layerNum; i++)
            {
                int currentLayer = i;

                s.Add("\t\t\t\t{");
                {
                    s.Add("\t\t\t\t\t\"name\": \"OpenBrushLayer " + (currentLayer + 1) + "\",");
                }

                s.Add(FINAL_LAYER_LIST[currentLayer]);

                s.Add("\t\t\t\t\t]");
                if (currentLayer < layerNum - 1)
                {
                    s.Add("\t\t\t\t},");
                }
                else
                {
                    s.Add("\t\t\t\t}");
                }
            }
            s.Add("            ]"); // end layers
            s.Add("        }");
            s.Add("    ]");
            s.Add("}");

            if (useZip)
            {
                saveJsonAsZip(writeFileName, string.Join("\n", s.ToArray()));
            }
            else
            {
                File.WriteAllText(writeFileName, string.Join("\n", s.ToArray()));
            }

            Debug.Log("*** Wrote " + writeFileName);
        }

        private static JSONNode getJsonFromZip(byte[] bytes)
        {
            MemoryStream fileStream = new MemoryStream(bytes, 0, bytes.Length);
            ZipFile zipFile = new ZipFile(fileStream);

            foreach (ZipEntry entry in zipFile)
            {
                if (Path.GetExtension(entry.Name).ToLower() == ".json")
                {
                    Stream zippedStream = zipFile.GetInputStream(entry);
                    StreamReader read = new StreamReader(zippedStream, true);
                    string json = read.ReadToEnd();
                    Debug.Log(json);
                    return JSON.Parse(json);
                }
            }

            return null;
        }

        private static void saveJsonAsZip(string fileName, string s)
        {
            MemoryStream memStreamIn = new MemoryStream();
            StreamWriter writer = new StreamWriter(memStreamIn);
            writer.Write(s);
            writer.Flush();
            memStreamIn.Position = 0;

            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            zipStream.SetLevel(4); // 0-9, 9 being the highest level of compression

            string fileNameBase = Path.GetFileNameWithoutExtension(fileName);

            ZipEntry newEntry = new ZipEntry(fileNameBase + ".json");
            newEntry.DateTime = System.DateTime.Now;

            zipStream.PutNextEntry(newEntry);

            StreamUtils.Copy(memStreamIn, zipStream, new byte[4096]);
            zipStream.CloseEntry();

            zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
            zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

            outputMemStream.Position = 0;

            using (FileStream file = new FileStream(fileName, FileMode.Create, System.IO.FileAccess.Write))
            {
                byte[] bytes = new byte[outputMemStream.Length];
                outputMemStream.Read(bytes, 0, (int)outputMemStream.Length);
                file.Write(bytes, 0, bytes.Length);
                outputMemStream.Close();
            }
        }

    }

}
