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

    public class ExportLatk : MonoBehaviour
    {

        [Header("~ Objects ~")]
        public Transform target;
        public LatkLayer layerPrefab;
        public LatkFrame framePrefab;
        public LatkStroke brushPrefab;
        public TextMesh textMesh;
        public AudioSource audio;
        public Animator animator;
        public Renderer floorRen;
        public Renderer[] animatorRen;

        public enum BrushMode { ADD, SURFACE, UNLIT };
        [Header("~ Brush Options ~")]
        public BrushMode brushMode = BrushMode.ADD;
        public Material[] brushMat;
        public Color mainColor = new Color(0.5f, 0.5f, 0.5f);
        public Color endColor = new Color(0.5f, 0.5f, 0.5f);
        public bool useEndColor = false;
        public bool fillMeshStrokes = false;
        public bool refineStrokes = false;
        public bool drawWhilePlaying = false;
        public float strokeLife = 5f;
        public bool killStrokes = false;
        public bool useCollisions = false;

        [Header("~ UI Options ~")]
        public float minDistance = 0.0001f;
        public float brushSize = 0.008f;
        [HideInInspector] public float pushSpeed = 0.01f;
        [HideInInspector] public float pushRange = 0.05f;
        [HideInInspector] public float eraseRange = 0.05f;
        [HideInInspector] public float colorPickRange = 0.05f;

        [Header("~ File Options ~")]
        public string readFileName = "LatkStrokes-saved.json";
        public bool readOnStart = false;
        public bool playOnStart = false;
        public string writeFileName = "LatkStrokes-saved.json";
        public bool useTimestamp = false;
        public bool writePressure = false;
        public bool createFrameWithLayer = false;
        public bool newLayerOnRead = false;

        // NONE plays empty frames as empty. 
        // WRITE copies last good frame into empty frame (will save out). 
        // DISPLAY holds last good frame but doesn't copy (won't save out).
        public enum FillEmptyMethod { NONE, WRITE, DISPLAY };
        [Header("~ Display Options ~")]
        public FillEmptyMethod fillEmptyMethod = FillEmptyMethod.DISPLAY;
        public float frameInterval = 12f;
        public int onionSkinRange = 5;
        public int drawTrailLength = 4;
        public float frameBrightNormal = 0.5f;
        public float frameBrightDim = 0.05f;

        [HideInInspector] public List<LatkLayer> layerList;
        [HideInInspector] public int currentLayer = 0;
        [HideInInspector] public bool isDrawing = false;
        [HideInInspector] public JSONNode jsonNode;
        [HideInInspector] public bool clicked = false;
        [HideInInspector] public bool isReadingFile = false;
        [HideInInspector] public bool isWritingFile = false;
        [HideInInspector] public bool isPlaying = false;
        [HideInInspector] public bool showOnionSkin = false;

        [HideInInspector] public bool armReadFile = false;
        [HideInInspector] public bool armWriteFile = false;

        [HideInInspector] public Vector3 lastHit = Vector3.zero;
        [HideInInspector] public Vector3 thisHit = Vector3.zero;
        [HideInInspector] public string url;

        [HideInInspector] public string statusText = "";

        private bool firstRun = true;
        private float lastFrameTime = 0f;
        private Renderer textMeshRen;
        private int rememberFrame = 0;
        private float markTime = 0f;
        private Vector2 brushSizeRange = new Vector2(0f, 1f);

        private float normalizedFrameInterval = 0f;
        private string clipName = "Take 001";
        private int clipLayer = 0;
        private float animVal = 0f;
        private Vector3 lastTargetPos = Vector3.zero;

        private float consoleUpdateInterval = 0f;
        private int debugTextCurrentFrame = 0;
        private int debugTextLastFrame = 0;
        private int longestLayer = 0;
        private float brushSizeDelta = 0f;

        private Matrix4x4 transformMatrix;
        private Matrix4x4 cameraTransformMatrix;

        public void updateTransformMatrix()
        {
            transformMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
        }

        public void updateCameraTransformMatrix()
        {
            cameraTransformMatrix = Matrix4x4.TRS(Camera.main.transform.position, Camera.main.transform.rotation, Camera.main.transform.localScale);
        }

        public Vector3 applyTransformMatrix(Vector3 p)
        {
            return transformMatrix.MultiplyPoint3x4(p);
        }

        public Vector3 applyCameraTransformMatrix(Vector3 p)
        {
            return cameraTransformMatrix.MultiplyPoint3x4(p);
        }

        int getLongestLayer()
        {
            int returns = 0;
            for (int i = 0; i < layerList.Count; i++)
            {
                if (layerList[i].frameList.Count > layerList[returns].frameList.Count) returns = i;
            }
            return returns;
        }

        void getBrushSizeRange()
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

        float getNormalizedBrushSize(float s)
        {
            return map(s, brushSizeRange.x, brushSizeRange.y, 0.1f, 1f);
        }

        float map(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }


        public IEnumerator writeLatkStrokes()
        {
            Debug.Log("*** Begin writing...");
            isWritingFile = true;

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

        Vector3 tween3D(Vector3 v1, Vector3 v2, float e)
        {
            v1 += (v2 - v1) / e;
            return v1;
        }

        bool checkEmptyFrame(int _index)
        {
            if (_index > 0 && layerList[currentLayer].frameList[_index].brushStrokeList.Count == 0)
            {
                layerList[currentLayer].frameList[_index].isDuplicate = true;
                Debug.Log("Empty frame " + _index);
            }
            else
            {
                layerList[currentLayer].frameList[_index].isDuplicate = false;
            }
            return layerList[currentLayer].frameList[_index].isDuplicate;
        }


        public LatkFrame getLastFrame()
        {
            LatkLayer layer = layerList[currentLayer];
            return layer.frameList[layer.currentFrame];
        }

        public LatkStroke getLastStroke()
        {
            LatkFrame frame = getLastFrame();
            LatkStroke stroke = frame.brushStrokeList[frame.brushStrokeList.Count - 1];
            return stroke;
        }

        public Vector3 getLastPoint()
        {
            LatkStroke stroke = getLastStroke();
            return stroke.points[stroke.points.Count - 1];
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

    public class LatkLayer : MonoBehaviour
    {

        public string name = "";
        public Vector3 parentPos = Vector3.zero;

        [HideInInspector] public List<LatkFrame> frameList;
        [HideInInspector] public int currentFrame = 0;
        [HideInInspector] public int previousFrame = 0;
        [HideInInspector] public float lerpSpeed = 0.5f;

        public void reset()
        {
            if (frameList != null)
            {
                for (int i = 0; i < frameList.Count; i++)
                {
                    Destroy(frameList[i].gameObject);
                }
                frameList = new List<LatkFrame>();
            }
        }

        public void deleteFrame()
        {
            try
            {
                Destroy(frameList[currentFrame].gameObject);
            }
            catch (UnityException e) { }
            frameList.RemoveAt(currentFrame);
            currentFrame--;
            if (currentFrame < 0) currentFrame = 0;
        }

        //private void Update() {
        //transform.position = Vector3.Lerp(transform.position, parentPos, lerpSpeed);
        //}

    }

    public class LatkFrame : MonoBehaviour
    {

        public Vector3 parentPos = Vector3.zero;
        public LatkLayer parentLayer;

        [HideInInspector] public List<LatkStroke> brushStrokeList;
        [HideInInspector] public bool isDuplicate = false;
        [HideInInspector] public bool isDirty = false;

        private void Awake()
        {
            //parentLayer = transform.parent.GetComponent<LatkLayer>();
        }

        private void Update()
        {
            if (isDirty) refresh();
        }

        public void showFrame(bool _b)
        {
            for (int i = 0; i < brushStrokeList.Count; i++)
            {
                brushStrokeList[i].gameObject.SetActive(_b);
                brushStrokeList[i].isDirty = isDirty;
            }

            //parentLayer.parentPos = new Vector3(parentPos.x, parentPos.y, parentPos.z);

            isDirty = false;
        }

        public void refresh()
        {
            if (brushStrokeList != null)
            {
                for (int i = 0; i < brushStrokeList.Count; i++)
                {
                    brushStrokeList[i].isDirty = true;
                }
            }

            //parentLayer.parentPos = new Vector3(parentPos.x, parentPos.y, parentPos.z);

            isDirty = false;
        }

        public void reset()
        {
            if (brushStrokeList != null)
            {
                for (int i = 0; i < brushStrokeList.Count; i++)
                {
                    Destroy(brushStrokeList[i].gameObject);
                }
                brushStrokeList = new List<LatkStroke>();
            }

            //parentPos = Vector3.zero;
        }

        public void setFrameBrightness(float _f)
        {
            for (int i = 0; i < brushStrokeList.Count; i++)
            {
                brushStrokeList[i].setBrushBrightness(_f);
            }
        }

    }

    public class LatkStroke : MonoBehaviour
    {

        public float brushSize = 0.008f;
        public Color brushColor = new Color(0.5f, 0.5f, 0.5f);
        public Color brushEndColor = new Color(0.5f, 0.5f, 0.5f);
        public float brushBrightness = 1f;
        public float birthTime = 0f;
        public float lifeTime = 5f;
        public bool selfDestruct = false;

        public bool isDirty = true;
        [HideInInspector] public LineRenderer lineRen;
        [HideInInspector] public List<Vector3> points;
        [HideInInspector] public Material mat;

        private int colorID;
        private MaterialPropertyBlock block;
        private int splitReps = 2;
        private int smoothReps = 10;
        private int reduceReps = 0;
        private float simplifyTolerance = 0.0001f;
        private MeshFilter mf;
        private Mesh mesh;
        private MeshRenderer meshRen;
        private bool armSimplify = false;

        private void Awake()
        {
            lineRen = GetComponent<LineRenderer>();
            colorID = Shader.PropertyToID("_Color");
            block = new MaterialPropertyBlock();
            try
            {
                mf = GetComponent<MeshFilter>();
                mesh = new Mesh();
                meshRen = GetComponent<MeshRenderer>();
            }
            catch (UnityException e) { }

            simplifyTolerance = brushSize / 10f;
        }

        void Start()
        {
            lineRen.enabled = false;
            if (mat) lineRen.sharedMaterial = mat;
            birthTime = Time.realtimeSinceStartup;
        }

        void Update()
        {
            if (isDirty) refresh();
            if (armSimplify) simplify();
        }

        public void refresh()
        {
            if (lineRen != null)
            {
                setBrushSize();
                setBrushColor();
            }

            if (points != null)
            {
                lineRen.enabled = points.Count > 1;
                lineRen.positionCount = points.Count;
                lineRen.SetPositions(points.ToArray());
            }

            isDirty = false;
        }

        public void setPoints(List<Vector3> p)
        {
            points = new List<Vector3>();
            for (int i = 0; i < p.Count; i++)
            {
                points.Add(new Vector3(p[i].x, p[i].y, p[i].z));
            }
        }

        public void addPoint(Vector3 p)
        {
            points.Add(p);
            isDirty = true;
        }

        public void setBrushSize()
        {
            lineRen.startWidth = lineRen.endWidth = brushSize;
            //lineRen.widthMultiplier = brushSize;
        }

        public void setBrushSize(float f)
        {
            brushSize = f;
            lineRen.startWidth = lineRen.endWidth = brushSize;
            //lineRen.widthMultiplier = brushSize;
        }

        public void setBrushColor()
        {
            brushMaterialColorChanger();
        }

        public void setBrushColor(Color c)
        {
            brushColor = c;
            brushMaterialColorChanger();
        }

        public void setBrushBrightness(float f)
        {
            brushBrightness = f;
            brushMaterialColorChanger();
        }

        public void brushMaterialColorChanger()
        {
            if (lineRen)
            {
                block.SetColor(colorID, changeBrightness(brushColor, brushBrightness));
                lineRen.SetPropertyBlock(block);
                if (meshRen) meshRen.SetPropertyBlock(block);
            }
        }

        Color changeBrightness(Color c, float f)
        {
            Color returns = c;
            returns.r *= f;
            returns.g *= f;
            returns.b *= f;
            return returns;
        }

        // ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~

        public List<Vector3> smoothStroke(List<Vector3> pl)
        {
            float weight = 18f;
            float scale = 1f / (weight + 2f);
            Vector3 lower, upper, center;

            for (int i = 1; i < pl.Count - 2; i++)
            {
                lower = pl[i - 1];
                center = pl[i];
                upper = pl[i + 1];

                center.x = (lower.x + weight * center.x + upper.x) * scale;
                center.y = (lower.y + weight * center.y + upper.y) * scale;
                center.z = (lower.z + weight * center.z + upper.z) * scale;

                pl[i] = center;
            }
            return pl;
        }

        public List<Vector3> splitStroke(List<Vector3> pl)
        {
            for (int i = 1; i < pl.Count; i += 2)
            {
                Vector3 center = pl[i];
                Vector3 lower = pl[i - 1];
                float x = (center.x + lower.x) / 2f;
                float y = (center.y + lower.y) / 2f;
                float z = (center.z + lower.z) / 2f;
                Vector3 p = new Vector3(x, y, z);
                pl.Insert(i, p);
            }
            return pl;
        }

        public List<Vector3> reduceStroke(List<Vector3> pl)
        {
            for (int i = 1; i < pl.Count - 1; i += 2)
            {
                pl.RemoveAt(i);
            }
            return pl;
        }

        public void refine()
        {
            for (int i = 0; i < splitReps; i++)
            {
                points = splitStroke(points);
                points = smoothStroke(points);
            }
            for (int i = 0; i < smoothReps - splitReps; i++)
            {
                points = smoothStroke(points);
            }
            for (int i = 0; i < reduceReps; i++)
            {
                points = reduceStroke(points);
            }
            isDirty = true;
        }

        public void simplify()
        {
            lineRen.Simplify(simplifyTolerance);

            Vector3[] temp = new Vector3[lineRen.positionCount];
            lineRen.GetPositions(temp);
            points = new List<Vector3>(temp);
            armSimplify = false;
        }

        public void randomize(float spread)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 r = new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), Random.Range(-spread, spread));
                points[i] += r;
            }
            isDirty = true;
        }

        public void moveStroke(Vector3 p)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i] += p;
            }
        }

    }

}
//#endif