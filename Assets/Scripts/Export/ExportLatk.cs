/*
LIGHTNING ARTIST TOOLKIT v1.1.0

The Lightning Artist Toolkit was developed with support from:
   Canada Council on the Arts
   Eyebeam Art + Technology Center
   Ontario Arts Council
   Toronto Arts Council
   
Copyright (c) 2021 Nick Fox-Gieg
http://fox-gieg.com

~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# 
#     http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
*/

//#if LATK_SUPPORTED
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SimpleJSON;
using ICSharpCode.SharpZipLibUnityPort.Core;
using ICSharpCode.SharpZipLibUnityPort.Zip;
using static TiltBrush.ExportUtils;

namespace TiltBrush
{

    public class ExportLatk
    {

        private string writeFileName = "test.zip";
        private bool writePressure = false;
        private bool useTimestamp = true;
        private Vector2 brushSizeRange = new Vector2(0f, 1f);
        private float consoleUpdateInterval = 0f;

        private void getBrushSizeRange()
        {
            List<float> allBrushSizes = new List<float>();

            for (int i = 0; i < layerList.Count; i++)
            {
                for (int j = 0; j < layerList[i].frameList.Count; j++)
                {
                    for (int k = 0; k < layerList[i].frameList[j].brushStrokeList.Count; k++)
                    {
                        allBrushSizes.Add(layerList[i].frameList[j].brushStrokeList[k].brushSize);
                    }
                }
            }

            allBrushSizes.Sort();
            brushSizeRange = new Vector2(allBrushSizes[0], allBrushSizes[allBrushSizes.Count - 1]);
        }

        private float getNormalizedBrushSize(float s)
        {
            return map(s, brushSizeRange.x, brushSizeRange.y, 0.1f, 1f);
        }

        private float map(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }

        private Vector3 tween3D(Vector3 v1, Vector3 v2, float e)
        {
            v1 += (v2 - v1) / e;
            return v1;
        }

        public IEnumerator writeLatkStrokes()
        {
            Debug.Log("*** Begin writing...");

            string ext = Path.GetExtension(writeFileName).ToLower();
            Debug.Log("Found extension " + ext);
            bool useZip = (ext == ".latk" || ext == ".zip");

            List<string> FINAL_LAYER_LIST = new List<string>();

            if (writePressure) getBrushSizeRange();

            for (int hh = 0; hh < layerList.Count; hh++)
            {
                currentLayer = hh;

                List<string> sb = new List<string>();
                List<string> sbHeader = new List<string>();
                sbHeader.Add("\t\t\t\t\t\"frames\":[");
                sb.Add(string.Join("\n", sbHeader.ToArray()));

                for (int h = 0; h < layerList[currentLayer].frameList.Count; h++)
                {
                    Debug.Log("Starting frame " + (layerList[currentLayer].currentFrame + 1) + ".");
                    layerList[currentLayer].currentFrame = h;

                    List<string> sbbHeader = new List<string>();
                    sbbHeader.Add("\t\t\t\t\t\t{");
                    sbbHeader.Add("\t\t\t\t\t\t\t\"strokes\":[");
                    sb.Add(string.Join("\n", sbbHeader.ToArray()));
                    for (int i = 0; i < layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList.Count; i++)
                    {
                        List<string> sbb = new List<string>();
                        sbb.Add("\t\t\t\t\t\t\t\t{");
                        float r = layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].brushColor.r;
                        float g = layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].brushColor.g;
                        float b = layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].brushColor.b;
                        sbb.Add("\t\t\t\t\t\t\t\t\t\"color\":[" + r + ", " + g + ", " + b + "],");

                        if (layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].points.Count > 0)
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t\t\"points\":[");
                            for (int j = 0; j < layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].points.Count; j++)
                            {
                                Vector3 pt = layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].transform.TransformPoint(layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].points[j]);
                                float x = pt.x;
                                float y = pt.y;
                                float z = pt.z;

                                string pressureVal = "1";
                                if (writePressure) pressureVal = "" + getNormalizedBrushSize(layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].brushSize);

                                if (j == layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList[i].points.Count - 1)
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

                        if (i == layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList.Count - 1)
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t}");
                        }
                        else
                        {
                            sbb.Add("\t\t\t\t\t\t\t\t},");
                        }

                        Debug.Log("Adding frame " + (layerList[currentLayer].currentFrame + 1) + ": stroke " + (i + 1) + " of " + layerList[currentLayer].frameList[layerList[currentLayer].currentFrame].brushStrokeList.Count + ".");

                        sb.Add(string.Join("\n", sbb.ToArray()));
                    }

                    statusText = "WRITING " + (layerList[currentLayer].currentFrame + 1) + " / " + layerList[currentLayer].frameList.Count;
                    Debug.Log("Ending frame " + (layerList[currentLayer].currentFrame + 1) + ".");
                    yield return new WaitForSeconds(consoleUpdateInterval);

                    List<string> sbFooter = new List<string>();
                    if (h == layerList[currentLayer].frameList.Count - 1)
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

            yield return new WaitForSeconds(consoleUpdateInterval);
            Debug.Log("+++ Parsing finished. Begin file writing.");
            yield return new WaitForSeconds(consoleUpdateInterval);

            List<string> s = new List<string>();
            s.Add("{");
            s.Add("\t\"creator\": \"unity\",");
            s.Add("\t\"grease_pencil\":[");
            s.Add("\t\t{");
            s.Add("\t\t\t\"layers\":[");

            for (int i = 0; i < layerList.Count; i++)
            {
                currentLayer = i;

                s.Add("\t\t\t\t{");
                if (layerList[currentLayer].name != null && layerList[currentLayer].name != "")
                {
                    s.Add("\t\t\t\t\t\"name\": \"" + layerList[currentLayer].name + "\",");
                }
                else
                {
                    s.Add("\t\t\t\t\t\"name\": \"UnityLayer " + (currentLayer + 1) + "\",");
                }

                s.Add(FINAL_LAYER_LIST[currentLayer]);

                s.Add("\t\t\t\t\t]");
                if (currentLayer < layerList.Count - 1)
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

            url = "";
            string tempName = "";
            if (useTimestamp)
            {
                string extO = "";
                if (useZip)
                {
                    extO = ".latk";
                }
                else
                {
                    extO = ".json";
                }
                tempName = writeFileName.Replace(extO, "");
                int timestamp = (int)(System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1))).TotalSeconds;
                tempName += "_" + timestamp + extO;
            }

            url = Path.Combine(Application.dataPath, tempName);

#if UNITY_ANDROID
        //url = "/sdcard/Movies/" + tempName;
        url = Path.Combine(Application.persistentDataPath, tempName);
#endif

#if UNITY_IOS
		url = Path.Combine(Application.persistentDataPath, tempName);
#endif

            if (useZip)
            {
                saveJsonAsZip(url, tempName, string.Join("\n", s.ToArray()));
            }
            else
            {
                File.WriteAllText(url, string.Join("\n", s.ToArray()));
            }

            Debug.Log("*** Wrote " + url);
            isWritingFile = false;

            yield return null;
        }

        JSONNode getJsonFromZip(byte[] bytes)
        {
            // https://gist.github.com/r2d2rigo/2bd3a1cafcee8995374f

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

        void saveJsonAsZip(string url, string fileName, string s)
        {
            // https://stackoverflow.com/questions/1879395/how-do-i-generate-a-stream-from-a-string
            // https://github.com/icsharpcode/SharpZipLib/wiki/Zip-Samples
            // https://stackoverflow.com/questions/8624071/save-and-load-memorystream-to-from-a-file

            MemoryStream memStreamIn = new MemoryStream();
            StreamWriter writer = new StreamWriter(memStreamIn);
            writer.Write(s);
            writer.Flush();
            memStreamIn.Position = 0;

            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

            string fileNameMinusExtension = "";
            string[] nameTemp = fileName.Split('.');
            for (int i = 0; i < nameTemp.Length - 1; i++)
            {
                fileNameMinusExtension += nameTemp[i];
            }

            ZipEntry newEntry = new ZipEntry(fileNameMinusExtension + ".json");
            newEntry.DateTime = System.DateTime.Now;

            zipStream.PutNextEntry(newEntry);

            StreamUtils.Copy(memStreamIn, zipStream, new byte[4096]);
            zipStream.CloseEntry();

            zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
            zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

            outputMemStream.Position = 0;

            using (FileStream file = new FileStream(url, FileMode.Create, System.IO.FileAccess.Write))
            {
                byte[] bytes = new byte[outputMemStream.Length];
                outputMemStream.Read(bytes, 0, (int)outputMemStream.Length);
                file.Write(bytes, 0, bytes.Length);
                outputMemStream.Close();
            }

            /*
            // Alternative outputs:
            // ToArray is the cleaner and easiest to use correctly with the penalty of duplicating allocated memory.
            byte[] byteArrayOut = outputMemStream.ToArray();

            // GetBuffer returns a raw buffer raw and so you need to account for the true length yourself.
            byte[] byteArrayOut = outputMemStream.GetBuffer();
            long len = outputMemStream.Length;
            */
        }

    }

}
//#endif