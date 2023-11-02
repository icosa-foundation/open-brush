using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.Linq;
using UnityEditor;


namespace TiltBrush.FrameAnimation
{
    public class AnimationUI_Manager : MonoBehaviour
    {




        int fps = 8;

        float frameOn = 0f;

        public int getFrameOn()
        {
            return Math.Clamp((int)frameOn, 0, getTimelineLength() - 1);
        }
        long start = 0, current = 0, time = 0;

        float frameOffset = 1.22f;
        int trackScrollOffset = 0;
        bool playing = false;





        // public struct frameLayer{
        //     public bool visible;
        //     public bool deleted;

        //     public CanvasScript canvas;
        // }

        public struct Frame
        {
            public bool visible;
            public bool deleted;

            public bool frameExists;
            public CanvasScript canvas;

            public MovementPathWidget animatedPath;

        }

        public struct Track
        {
            public List<Frame> Frames;
            public bool visible;
            public bool deleted;

        }
        
         public List<Track> timeline;


        public Frame newFrame(CanvasScript canvas)
        {
            Frame thisframeLayer;
            thisframeLayer.canvas = canvas;
            thisframeLayer.visible = (bool)App.Scene.IsLayerVisible(canvas);
            thisframeLayer.deleted = false;
            thisframeLayer.frameExists = true;
            thisframeLayer.animatedPath = null;
           
            return thisframeLayer;
        }

        Track newTrack()
        {
            Track thisFrame;
            thisFrame.Frames = new List<Frame>();

            thisFrame.visible = true;
            thisFrame.deleted = false;
            return thisFrame;
        }


        [SerializeField] public GameObject timelineRef;
        [SerializeField] public GameObject timelineSliderPosition;

        [SerializeField] public GameObject timelineNotchPrefab;
        [SerializeField] public GameObject timelineFramePrefab;

        [SerializeField] public GameObject timelineField;
        [SerializeField] public GameObject textRef;

        [SerializeField] public GameObject deleteFrameButton;
        [SerializeField] public GameObject frameButtonPrefab;




        [SerializeField] public GameObject layersPanel;

        GameObject animationPathCanvas;


        // [SerializeField] public GameObject captureRig;


        bool animationMode = true;


        // Visual size of frame on timeline
        float sliderFrameSize = 0.12f;

        float timelineOffset = 0.0f;
        public List<GameObject> timelineNotches;
        public List<GameObject> timelineFrameObjects;

       

        public struct animatedModel
        {
            public GameObject gameObject;
            public string name;
        }

        public List<animatedModel> animatedModels;


        // List<CanvasScript> Frames = new List<CanvasScript>();

        // Start is called before the first frame update
        void Start()
        {

            // nextFrame = App.Scene.addFrame();

            print("START ANIM");
        }
        void Awake()
        {



            App.Scene.animationUI_manager = this;
            // resetTimeline();


        }
        public void startTimeline()
        {
            timeline = new List<Track>();
            animatedModels = new List<animatedModel>();

            Track mainTrack = newTrack();
            Frame originFrame = newFrame(App.Scene.m_MainCanvas);


            mainTrack.Frames.Add(originFrame);

            timeline.Add(mainTrack);

            App.Scene.animationUI_manager = this;

            focusFrame(0);

            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();

            resetTimeline();

            animationPathCanvas = new GameObject("AnimationPaths");
            animationPathCanvas.transform.parent = App.Scene.gameObject.transform;
            animationPathCanvas.AddComponent<CanvasScript>();


            print("START TIMELINE");
        }
        public void init()
        {

            print("INIT");
        }


        private void hideFrame(int hidingFrame, int frameOn)
        {
            Debug.Log(" HIDE FRAME " + hidingFrame);
            foreach (Track track in timeline)
            {

                if (hidingFrame >= track.Frames.Count ){continue;}
                if (frameOn < track.Frames.Count && track.Frames[hidingFrame].canvas.Equals(track.Frames[frameOn].canvas)) continue;

                Frame thisFrame = track.Frames[hidingFrame];


                App.Scene.HideLayer(thisFrame.canvas);
                thisFrame.visible = false;
                track.Frames[hidingFrame] = thisFrame;
            }

        }


        int previousShowingFrame = -1;
        private void showFrame(int frameIndex)
        {




            print("SHOWING FRAME ++ " + frameIndex);

            if (previousShowingFrame == frameIndex) return;



            for (int i =0;i<timeline.Count;i++)
            {
                if (frameIndex >= timeline[i].Frames.Count ){continue;}
                Frame thisFrame = timeline[i].Frames[frameIndex];

                
                 Debug.Log("CANVAS SHOWN ");
                 Debug.Log(thisFrame.canvas);
                thisFrame.visible = true;

                Debug.Log("ON SHOWING IDX " + i);
            

                if ( timeline[i].visible && !thisFrame.deleted)
                {
            
                    App.Scene.ShowLayer(thisFrame.canvas);
                    thisFrame.visible = true;
                }
                else
                {
                 
                    App.Scene.HideLayer(thisFrame.canvas);
                    thisFrame.visible = false;
                }
                timeline[i].Frames[frameIndex] = thisFrame;
            }

            previousShowingFrame = frameIndex;


        }

        public void refreshAnimatedModelUI()
        {
            this.gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().ResetUI();
            resetTimeline();
        }

        public void addAnimationPath(MovementPathWidget pathwidget)
        {
            

            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(animationPathCanvas.transform);

            pathwidget.setPathAnimation(true);

            (int, int) Loc = getCanvasIndex(App.Scene.ActiveCanvas);



            CanvasScript origCanvas = timeline[Loc.Item2].Frames[Loc.Item1].canvas;

            if (timeline[Loc.Item2].Frames[Loc.Item1].animatedPath != null)
            {
                TiltBrush.WidgetManager.m_Instance.DeleteMovementPath(timeline[Loc.Item2].Frames[Loc.Item1].animatedPath);
            }

            int i = 0;

            List<Frame> framesChanging = new List<Frame>();
            while (
                Loc.Item1 + i < timeline[Loc.Item2].Frames.Count &&
                timeline[Loc.Item2].Frames[Loc.Item1 + i].canvas.Equals(origCanvas)
                )
            {

                i++;
            }



            for (int c = 0; c < i; c++)
            {

                Frame changingFrame = timeline[Loc.Item2].Frames[Loc.Item1 + c];

                changingFrame.animatedPath = pathwidget;
                timeline[Loc.Item2].Frames[Loc.Item1 + c] = changingFrame;
            }


            Debug.Log("FINISHED ANIMATED PATH ");
            Debug.Log(timeline[Loc.Item2].Frames[Loc.Item1].animatedPath);


            resetTimeline();


        }
        public void AddLayerRefresh(CanvasScript canvasAdding)
        {


            // int numLayers = App.Scene.m_LayerCanvases.Count;
            // int created = 0;
            // print("THIS TIMELINE," + timeline);
            //  print("THIS TIMELINE COUNT," + timeline.Count);
            // for (int i =0 ; i < timeline.Count; i++){

            //     if (i == frameOn){

            //         frameLayer addingLayer = newFrameLayer(canvasAdding);
            //         timeline[i].layers.Add(addingLayer);
            //         created ++;
            //         print("CREATED,"+ created);

            //     }else{
            //         CanvasScript newCanvas = App.Scene.AddCanvas();
            //         frameLayer addingLayer = newFrameLayer(newCanvas);
            //         timeline[i].layers.Add(addingLayer);
            //         created ++;
            //         print("CREATED,"+ created);
            //     }


            // }

            Track addingTrack = newTrack();


            for (int i = 0; i < getTimelineLength(); i++)
            {
              

                Frame addingFrame = newFrame(canvasAdding);
                
                addingTrack.Frames.Add(addingFrame);
                // if (i == frameOn){

                //     Frame addingFrame = newFrame(canvasAdding);
                //     addingTrack.Frames.Add(addingFrame);


                // }else{
                //     Frame addingFrame = newFrame(App.Scene.AddCanvas());
                //     addingTrack.Frames.Add(addingFrame);


                // }


            }
            timeline.Add(addingTrack);

            print("ADDED LAYER REFRESH");
            printTimeline();
        }


        
        public (int, int) getCanvasIndex(CanvasScript canvas)
        {

            for (int trackNum = 0; trackNum < timeline.Count; trackNum++)
            {

                for (int frameNum = 0; frameNum < timeline[trackNum].Frames.Count; frameNum++)
                {

                    if (canvas.Equals(timeline[trackNum].Frames[frameNum].canvas))
                    {
                        return (frameNum, trackNum);
                    };

                }

            }
            return (-1, -1);
        }



        public CanvasScript getTimelineCanvas(int frameIndex, int trackIndex)
        {

            if (timeline.Count > frameIndex)
            {
                if (timeline[trackIndex].Frames.Count > frameIndex)
                {
                    return timeline[trackIndex].Frames[frameIndex].canvas;
                }
            }
            return App.Scene.MainCanvas;


        }

        public List<List<CanvasScript>> getTrackCanvases()
        {
            List<List<CanvasScript>> timelineCavses = new List<List<CanvasScript>>();

            for (int l = 0; l < getTimelineLength(); l++)
            {
        

                List<CanvasScript> canvasFrames = new List<CanvasScript>();

                for (int i = 0; i < timeline.Count; i++)
                {

                    if (l >= timeline[i].Frames.Count){continue;};

                    canvasFrames.Add(timeline[i].Frames[l].canvas);
                };

                timelineCavses.Add(canvasFrames);
            }


            return timelineCavses;
        }


        public void printTimeline()
        {
            String timelineString = "";

            for (int i = 0; i < timeline.Count; i++)
            {
                timelineString += " Track-" + i + " ";


                for (int l = 0; l < timeline[i].Frames.Count; l++)
                {

                    timelineString += "[Frame " + timeline[i].Frames[l].deleted + "] ";
                }

                timelineString += "\n";
            }
            print(timelineString);



        }

        public void updateLayerVisibilityRefresh(CanvasScript canvas)
        {

            bool visible = canvas.gameObject.activeSelf;

            (int, int) canvasIndex = getCanvasIndex(canvas);

            if (canvasIndex.Item1 != -1)
            {


                Track thisTrack = timeline[canvasIndex.Item2];
                thisTrack.visible = visible;


                for (int i = 0; i < thisTrack.Frames.Count; i++)
                {



                    Frame changingFrame = thisTrack.Frames[i];
                    changingFrame.visible = visible;
                    // App.Scene.HideLayer(changingLayer.canvas);

                    thisTrack.Frames[i] = changingFrame;

                }

                timeline[canvasIndex.Item2] = thisTrack;
            }

        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas)
        {

            (int, int) canvasIndex = getCanvasIndex(canvas);

            print(" DELETING LAYER TRACK " + canvasIndex.Item2);

            if (canvasIndex.Item1 != -1)
            {

                Track thisTrack = timeline[canvasIndex.Item2];
                thisTrack.deleted = true;

                for (int i = 0; i < thisTrack.Frames.Count; i++)
                {




                    Frame deletingFrame = thisTrack.Frames[i];
                    deletingFrame.deleted = true;
                    App.Scene.HideLayer(deletingFrame.canvas);

                    thisTrack.Frames[i] = deletingFrame;

                }
                timeline[canvasIndex.Item2] = thisTrack;
            }
            updateTrackScroll();
        }


        public void SquashLayerRefresh(CanvasScript SquashedLayer, CanvasScript DestinationLayer)
        {

            // (int,int) canvasIndex = getCanvasIndex(canvas);

            // print(" DELETING LAYER TRACK " + canvasIndex.Item2);


            (int, int) SquashedCoord = getCanvasIndex(SquashedLayer);
            (int, int) DestinationCoord = getCanvasIndex(DestinationLayer);

            Stroke[] m_OriginalStrokes;

            if (SquashedCoord.Item1 != -1 && DestinationCoord.Item1 != -1)
            {

                for (int i = 0; i < getTimelineLength(); i++)
                {

                 
                        
                    if (i != frameOn)
                    {

                        m_OriginalStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas == timeline[SquashedCoord.Item2].Frames[i].canvas).ToArray();

                        foreach (var stroke in m_OriginalStrokes)
                        {

                            stroke.SetParentKeepWorldPosition(timeline[DestinationCoord.Item2].Frames[i].canvas);

                        }
                    }

                    Frame squashingFrame = timeline[SquashedCoord.Item2].Frames[i];
                    squashingFrame.deleted = true;

                    timeline[SquashedCoord.Item2].Frames[i] = squashingFrame;

                    App.Scene.HideLayer(timeline[SquashedCoord.Item2].Frames[i].canvas);


                }
            }

        }

        public void DestroyLayerRefresh(CanvasScript canvasAdding)
        {



        }


        public int getTimelineLength(){
            int maxLength = 0;

            for (int t = 0; t < timeline.Count; t++){
                if (timeline[t].Frames.Count > maxLength){

                    maxLength = timeline[t].Frames.Count;

                }
        

            }
            return maxLength;
        }


        public void resetTimeline()
        {


            print("RESET TIMELINE");
            print(timelineNotches);


            if (timelineNotches != null)
            {
                foreach (var notch in timelineNotches)
                {
                    Destroy(notch);

                }
            }
            if (timelineFrameObjects != null)
            {
                foreach (var frame in timelineFrameObjects)
                {
                    Destroy(frame);

                }
            }
            foreach (Transform thisObj in timelineField.transform)
            {
                GameObject.Destroy(thisObj.gameObject);
            }

            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();



            int timelineLength = getTimelineLength();
            for (int f = 0; f < timelineLength; f++)
            {





                GameObject newNotch = Instantiate(timelineNotchPrefab);

                newNotch.transform.FindChild("Num").GetComponent<TextMeshPro>().text = "" + f;

                newNotch.transform.SetParent(timelineRef.transform);


                newNotch.SetActive(false);

                timelineNotches.Add(newNotch);





                GameObject newFrame = Instantiate(timelineFramePrefab, timelineField.transform, false);



                // newFrame.transform.SetParent(timelineField.transform);
                timelineFrameObjects.Add(newFrame);

                newFrame.name = "FrameContainer_" + f.ToString();


                GameObject frameWrapper = newFrame.transform.GetChild(0).gameObject;

                int numDeleted = 0;

                for (int i = frameWrapper.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(frameWrapper.transform.GetChild(i).gameObject);
                }
                // for(int i = 0; i < frameWrapper.transform.childCount; i++)
                // {

                //     frameWrapper.transform.GetChild(i).gameObject.SetActive(false);
                // }

                for (int i = 0; i < timeline.Count; i++)
                {

                    // // Square ui element on each timeline step
                    // bool backgroundBackConnect = (f > 0);
                    // bool backgroundForwardConnect = (f < timeline[i].Frames.Count - 1);


                    // Empty timeslot in timeline
                    if (f >= timeline[i].Frames.Count) {


                        var newButton = Instantiate(frameButtonPrefab, frameWrapper.transform, false);
                        var frameButton = newButton.transform.GetChild(0);

                        frameButton.GetComponent<MeshRenderer>().enabled = false;


                        // frameButton.localScale = new Vector3(1f,1f,7.88270617f);
                        frameButton.localPosition = new Vector3(0.00538007962f, 0.449999988f - frameOffset * i, -0.963263571f);
                        frameButton.gameObject.SetActive(true);


                        frameButton.gameObject.GetComponent<FrameButton>().setButtonCoordinate(i, f);

                        
                         for (int o = 0; o < frameButton.GetChildCount(); o++)
                        {

                            frameButton.GetChild(o).gameObject.SetActive(false);

                        }

                        int backBox = 6;
                        frameButton.GetChild(backBox).gameObject.SetActive(true);

                        Color backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f);

                        frameButton.GetChild(backBox).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox+1).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox+2).gameObject.GetComponent<SpriteRenderer>().color = backColor;



                        bool backgroundBackConnect = (f > 0 && f - 1 > timeline[i].Frames.Count - 1);
                        bool backgroundForwardConnect = f < getTimelineLength() - 1 && (f + 1 <  timeline[i].Frames.Count && timeline[i].Frames[f + 1].canvas == null);

                        if (backgroundBackConnect)
                        {
                
                            frameButton.GetChild(backBox+1).gameObject.SetActive(true);
                        }

                        if (backgroundForwardConnect)
                        {
                            frameButton.GetChild(backBox+2).gameObject.SetActive(true);
                        }

                        
                    }
                    // Timestamp with frame
                    else{
                    numDeleted += timeline[i].Frames[f].deleted ? 1 : 0;

                    int layerOn = i - numDeleted;

                    if (layerOn < timeline.Count && !timeline[i].Frames[f].deleted)
                    {



                     

                        // var frameButton =  frameWrapper.transform.GetChild(layerOn);
                        var newButton = Instantiate(frameButtonPrefab, frameWrapper.transform, false);
                        var frameButton = newButton.transform.GetChild(0);

                        frameButton.GetComponent<MeshRenderer>().enabled = false;


                        // frameButton.localScale = new Vector3(1f,1f,7.88270617f);
                        frameButton.localPosition = new Vector3(0.00538007962f, 0.449999988f - frameOffset * i, -0.963263571f);
                        frameButton.gameObject.SetActive(true);


                        frameButton.gameObject.GetComponent<FrameButton>().setButtonCoordinate(i, f);

                


                        // Hide all ui indicators first
                        for (int o = 0; o < frameButton.GetChildCount(); o++)
                        {

                            frameButton.GetChild(o).gameObject.SetActive(false);


                            if (timeline[i].Frames[f].animatedPath != null && frameButton.GetChild(o).gameObject.GetComponent<SpriteRenderer>() != null)
                            {
                                Debug.Log("BUT COLOUR " + frameButton.GetChild(o).gameObject.name);
                                frameButton.GetChild(o).gameObject.GetComponent<SpriteRenderer>().color = new Color(92f / 255f, 52f / 255f, 237f / 255f);
                            }

                        }
            
                        bool backwardsConnect = (f > 0 && timeline[i].Frames[f].canvas.Equals(timeline[i].Frames[f - 1].canvas));
                        bool forwardConnect = f < timeline[i].Frames.Count - 1 && timeline[i].Frames[f].canvas.Equals(timeline[i].Frames[f + 1].canvas);
                        bool filled = timeline[i].Frames[f].canvas.BatchManager.GetNumBatchPools() > 0;

                        frameButton.GetChild(Convert.ToInt32(filled)).gameObject.SetActive(true);

                        int backBox = 6;
                        frameButton.GetChild(backBox).gameObject.SetActive(true);


                        // Set behind colours depending on if frame is active
                        Color backColor;
                        if (timeline[i].Frames[f].canvas.Equals(App.Scene.ActiveCanvas)){
                            backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                        }else{
                            backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f);
                        }

                        
                        frameButton.GetChild(backBox).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox+1).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox+2).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        

                     
           

                        if (backwardsConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 2).gameObject.SetActive(true);
                            frameButton.GetChild(backBox+1).gameObject.SetActive(true);
                        }

                        if (forwardConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 4).gameObject.SetActive(true);
                            frameButton.GetChild(backBox+2).gameObject.SetActive(true);
                        }


                        // if (backgroundBackConnect)
                        // {
                
                        //     frameButton.GetChild(backBox+1).gameObject.SetActive(true);
                        // }

                        // if (backgroundForwardConnect)
                        // {
                        //     frameButton.GetChild(backBox+2).gameObject.SetActive(true);
                        // }



                    }
                    }
                }


            }




            updateTimelineSlider();
            updateTimelineNob();
            updateTrackScroll();
        }

        public void updateTrackScroll(int scrollOffset, float scrollHeight)
        {


            trackScrollOffset = scrollOffset;
            for (int i = 0; i < timelineFrameObjects.Count; i++)
            {



                GameObject frameWrapper = timelineFrameObjects[i].transform.GetChild(0).gameObject;




                for (int c = 0; c < frameWrapper.transform.GetChildCount(); c++)
                {



                    GameObject frameObject = frameWrapper.transform.GetChild(c).gameObject;

                    Vector3 thisPos = frameObject.transform.localPosition;
                    thisPos.y = -scrollOffset * frameOffset;
                    frameObject.transform.localPosition = thisPos;


                    int thisFrameOffset = c + scrollOffset;

                    if (thisFrameOffset >= 7 || thisFrameOffset < 0)
                    {
                        frameObject.SetActive(false);
                    }
                    else
                    {
                        frameObject.SetActive(true);
                    }
                }

            }
        }

        public void updateTrackScroll()
        {

            int scrollOffsetLocal = trackScrollOffset;
            print("SCROLLOFFSET HERE " + scrollOffsetLocal);
            for (int i = 0; i < timelineFrameObjects.Count; i++)
            {



                GameObject frameWrapper = timelineFrameObjects[i].transform.GetChild(0).gameObject;

#if UNITY_EDITOR
                EditorGUIUtility.PingObject(frameWrapper);
#endif

                print("START LOOP == " + frameWrapper.transform.GetChildCount());
                for (int c = 0; c < frameWrapper.transform.GetChildCount(); c++)
                {



                    GameObject frameObject = frameWrapper.transform.GetChild(c).gameObject;

                    Vector3 thisPos = frameObject.transform.localPosition;
                    thisPos.y = -scrollOffsetLocal * frameOffset;
                    frameObject.transform.localPosition = thisPos;


                    int thisFrameOffset = c + scrollOffsetLocal;

                    print("HERE : " + c + "  " + thisFrameOffset);
                    print(thisFrameOffset >= 7);
                    print(thisFrameOffset < 0);
                    if (thisFrameOffset >= 7 || thisFrameOffset < 0)
                    {
                        print("CHOOSING FALSE");
                        frameObject.SetActive(false);
                    }
                    else
                    {
                        print("CHOOSING TRUE");
                        frameObject.SetActive(true);
                    }
                }
                print("END LOOP == ");

            }
        }
        public void updateTimelineSlider()
        {

            float meshLength = timelineRef.GetComponent<TimelineSlider>().m_MeshScale.x;
            float startX = -meshLength / 2f - timelineOffset * meshLength;


            int timelineLength =  getTimelineLength();
            for (int f = 0; f < getTimelineLength(); f++)
            {
             

                float thisOffset = ((float)(f)) * sliderFrameSize * meshLength;

                float notchOffset = startX + ((float)(f)) * sliderFrameSize * meshLength;
                if (timelineNotches.ElementAtOrDefault(f) != null)
                {
                    // logic

                    GameObject notch = timelineNotches[f];



                    notch.transform.localPosition = new Vector3(notchOffset, 0, 0);
                    notch.transform.localRotation = Quaternion.identity;


                    notch.SetActive(notchOffset >= -meshLength * 0.5 && notchOffset <= meshLength * 0.5);
                }

                if (timelineFrameObjects.ElementAtOrDefault(f) != null)
                {
                    Vector3 newPosition = timelineFrameObjects[f].transform.localPosition;
                    float width = timelineFrameObjects[f].transform.GetChild(0).localScale.x;

                    newPosition.x = thisOffset - timelineOffset * meshLength - width * 0.5f;

                    timelineFrameObjects[f].transform.localPosition = new Vector3(newPosition.x, 0, 0); ;
                    timelineFrameObjects[f].transform.localRotation = Quaternion.identity;
                    timelineFrameObjects[f].SetActive(newPosition.x >= -0.1 && newPosition.x <= meshLength - width);
                }

            }


        }

        public void selectTimelineFrame(int trackNum, int frameNum)
        {



            App.Scene.ActiveCanvas = timeline[trackNum].Frames[frameNum].canvas;
            frameOn = frameNum;
            focusFrame(frameNum);

            resetTimeline();
            updateTimelineNob();
        }
        public void updateTimelineNob()
        {

            float newVal = (float)(frameOn - 0.01) * sliderFrameSize - timelineOffset;



            if (newVal >= 0.9f)
            {
                timelineOffset += newVal - 0.9f;


            }
            if (newVal <= 0.1f)
            {
                timelineOffset += newVal - 0.1f;

            }

            float max = sliderFrameSize * (float)getTimelineLength() - 1;
            timelineOffset = Math.Clamp(timelineOffset, 0, max < 0 ? 0 : max);

            float clampedval = (float)newVal;
            clampedval = Math.Clamp(clampedval, 0, 1);

            timelineRef.GetComponent<TimelineSlider>().setSliderValue(clampedval);
        }
        public void updateFrameInfo()
        {
            textRef.GetComponent<TextMeshPro>().text = (frameOn.ToString("0.00")) + ":" + getTimelineLength();
        }
        public void updateUI(bool timelineInput = false)
        {
            updateFrameInfo();
            updateTimelineSlider();
            if (!timelineInput) updateTimelineNob();

            deleteFrameButton.GetComponent<RemoveKeyFrameButton>().SetButtonAvailable(
                !(
                getFrameOn() < timeline[0].Frames.Count &&
                 timeline[0].Frames[getFrameOn()].canvas.Equals( timeline[0].Frames[0].canvas) 
                )
                );

        }

        public void focusFrameNum(int frameNum)
        {
            focusFrame(frameNum);
        }

        private void focusFrame(int FrameIndex, bool timelineInput = false)
        {



            for (int i = 0; i < getTimelineLength(); i++)
            {

                // if (i >= timeline[i].Frames.Count){continue;};

                if (i == FrameIndex)
                {
                    // frameOn = i;
                    continue;
                }
                print("HIDING IN FOCUS FRAME");
                hideFrame(i, FrameIndex);

            }



            App.Scene.m_LayerCanvases = new List<CanvasScript>();
            //  App.Scene.m_LayerCanvases[i - 1] = 
            for (int i = 0; i < timeline.Count; i++)
            {

                if (FrameIndex >= timeline[i].Frames.Count ) {
                        //  App.Scene.m_LayerCanvases[i - 1] = null;
                         continue;
                }

                if (i == 0)
                {
                    App.Scene.m_MainCanvas = timeline[i].Frames[FrameIndex].canvas;
                    continue;
                }

                print("INFO " + i + " " + App.Scene.m_LayerCanvases.Count + " ");

                App.Scene.m_LayerCanvases.Add(timeline[i].Frames[FrameIndex].canvas);

            }




            (int, int) previousActiveCanvas = getCanvasIndex(App.Scene.ActiveCanvas);

            print("PREV CANV INDEX " + previousActiveCanvas.Item1 + " " + previousActiveCanvas.Item2);

            if (previousActiveCanvas.Item2 != -1 && FrameIndex < timeline[previousActiveCanvas.Item2].Frames.Count)
            {
                App.Scene.ActiveCanvas = timeline[previousActiveCanvas.Item2].Frames[FrameIndex].canvas;
            }


            showFrame(FrameIndex);

            // Debug.Log("SHOWING FRAME " + FrameIndex);
            // Debug.Log(timeline[0].Frames[FrameIndex].canvas);

            updateUI(timelineInput);

        }
        public void removeKeyFrame()
        {

            if (frameOn <= 0) return;

            print("BEFORE REMOVE");
            printTimeline();

            int previousTrackActive = getCanvasIndex(App.Scene.ActiveCanvas).Item2;


            for (int l = 0; l < timeline.Count; l++)
            {



                //App.Scene.destroyCanvas(timeline[frameOn].layers[l].canvas);
                App.Scene.HideCanvas(timeline[l].Frames[getFrameOn()].canvas);

                Frame removingFrame = timeline[l].Frames[getFrameOn()];
                removingFrame.deleted = true;

                timeline[l].Frames.RemoveAt(getFrameOn());
            }



            frameOn = Math.Clamp(frameOn, 0, timeline.Count - 1);

            print("AFTER REMOVE");
            printTimeline();

            App.Scene.ActiveCanvas = timeline[previousTrackActive].Frames[getFrameOn()].canvas;
            focusFrame(getFrameOn());

            resetTimeline();

        }
        public void clearTimeline()
        {
            timeline = new List<Track>();
        }
        public void addLayersRaw(String name, bool visible, bool mainTrack = false)
        {


            Track addingTrack = newTrack();

            for (int i = 0; i < getTimelineLength(); i++)
            {
           

                if (mainTrack && i == 0)
                {

                    App.Scene.MainCanvas.gameObject.SetActive(visible);
                    Frame addingFrame = newFrame(App.Scene.MainCanvas);
                    addingTrack.Frames.Add(addingFrame);

                }
                else
                {
                    CanvasScript newCanvas = App.Scene.AddCanvas();
                    newCanvas.gameObject.name = name;
                    newCanvas.gameObject.SetActive(visible);
                    Frame addingFrame = newFrame(newCanvas);
                    addingTrack.Frames.Add(addingFrame);


                }




            }
            timeline.Add(addingTrack);

        }

        public void addKeyFrame(){

        }

        public void extendKeyFrame()
        {

            print("BEFORE  ADD");

            (int, int) index = getCanvasIndex(App.Scene.ActiveCanvas);

            print("ADDING LAYER HERE - " + index.Item2);
                        // Frame addingFrame = newFrame(App.Scene.AddCanvas());

            Frame addingFrame = newFrame(timeline[index.Item2].Frames[index.Item1].canvas);
                        // CanvasScript newCanvas = App.Scene.AddCanvas();
                        // frameLayer addingLayer = newFrameLayer(newCanvas);
            addingFrame.deleted = timeline[index.Item2].Frames[0].deleted;
            addingFrame.animatedPath = timeline[index.Item2].Frames[index.Item1].animatedPath;
            print("ADDING LAYER - " + index.Item1);

            timeline[index.Item2].Frames.Insert(index.Item1 + 1, addingFrame);

            // for (int l = 0; l < timeline.Count; l++)
            // {

            //     if (index.Item2 == l){

            //             print("ADDING LAYER HERE - " + l);
            //             // Frame addingFrame = newFrame(App.Scene.AddCanvas());

            //             Frame addingFrame = newFrame(timeline[l].Frames[timeline[l].Frames.Count - 1].canvas);
            //             // CanvasScript newCanvas = App.Scene.AddCanvas();
            //             // frameLayer addingLayer = newFrameLayer(newCanvas);
            //             addingFrame.deleted = timeline[l].Frames[0].deleted;
            //             addingFrame.animatedPath = timeline[l].Frames[timeline[l].Frames.Count - 1].animatedPath;
            //             print("ADDING LAYER - " + l);

            //             timeline[l].Frames.Insert(getFrameOn() + 1, addingFrame);
            //     }


            // }


            ;

            frameOn++;
            focusFrame((int)frameOn);

            print("TIMELINE SIZE -" + getTimelineLength());


            resetTimeline();



        }

        public void reduceKeyFrame(){
              (int, int) index = getCanvasIndex(App.Scene.ActiveCanvas);

                int frameLength = getFrameLength(index.Item2,index.Item1);

                if (frameLength > 1){
                        timeline[index.Item2].Frames.RemoveAt(index.Item1);
                        resetTimeline();
                }   

            

        }


        public void duplicateKeyFrame()
        {



            (int, int) index = getCanvasIndex(App.Scene.ActiveCanvas);

            CanvasScript newCanvas = App.Scene.AddCanvas();


            (int, int) canvasCoord = (getFrameOn(), index.Item2);
            CanvasScript oldCanvas = getTimelineCanvas(canvasCoord.Item1, canvasCoord.Item2);






            List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                        .Where(x => x.Canvas
                        ==
                            oldCanvas
                            ).ToList();

            List<Stroke> newStrokes = oldStrokes
            .Select(stroke => SketchMemoryScript.m_Instance.DuplicateStroke(
                stroke, App.Scene.SelectionCanvas, null))
            .ToList();

            foreach (var stroke in newStrokes)
            {
                switch (stroke.m_Type)
                {
                    case Stroke.Type.BrushStroke:
                        {
                            BaseBrushScript brushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
                            if (brushScript)
                            {
                                brushScript.HideBrush(false);
                            }
                        }
                        break;
                    case Stroke.Type.BatchedBrushStroke:
                        {
                            stroke.m_BatchSubset.m_ParentBatch.EnableSubset(stroke.m_BatchSubset);
                        }
                        break;
                    default:
                        Debug.LogError("Unexpected: redo NotCreated duplicate stroke");
                        break;
                }
                TiltMeterScript.m_Instance.AdjustMeter(stroke, up: true);

                stroke.SetParentKeepWorldPosition(newCanvas);
            }


            Frame addingFrame = newFrame(newCanvas);


            addingFrame.deleted = false;

            timeline[canvasCoord.Item2].Frames[canvasCoord.Item1 + 1] = addingFrame;

            int i = 2;
            while (canvasCoord.Item1 + i < timeline[canvasCoord.Item2].Frames.Count &&
             timeline[canvasCoord.Item2].Frames[canvasCoord.Item1 + i].canvas.Equals(oldCanvas)
             )
            {
                timeline[canvasCoord.Item2].Frames[canvasCoord.Item1 + i] = newFrame(newCanvas);
                i++;
            }

            resetTimeline();


           


        }



        public void timelineSlide(float Value)
        {

            this.gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().OnDisable();

            frameOn = ((float)(Value + timelineOffset) / sliderFrameSize);



            frameOn = frameOn >= getTimelineLength() ? getTimelineLength() : frameOn;
            frameOn = frameOn < 0 ? 0 : frameOn;

            print("T SLIDE frameoN- " + frameOn);
            focusFrame(getFrameOn(), true);
            updateLayerTransforms();

            // Scrolling the timeline
            print("TIMELINE SCROLLING " + Value);
            if (Value < 0.1f)
            {
                timelineOffset -= 0.05f;
                print("SCROLL LEFT " + timelineOffset);
            }
            if (Value > 0.9f)
            {
                timelineOffset += 0.05f;
                print("SCROLL RIGHT " + timelineOffset);
            }
            float max = sliderFrameSize * (float)getTimelineLength() - 1;
            timelineOffset = Math.Clamp(timelineOffset, 0, max < 0 ? 0 : max);


            updateTimelineSlider();

            this.gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().OnEnable();
        }

        public void startAnimation()
        {
            start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            playing = true;

            this.gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().setAnimating(playing);
        }
        public void stopAnimation()
        {
            playing = false;


            this.gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().setAnimating(playing);
        }
        public bool getPlaying()
        {
            return playing;
        }
        public void toggleAnimation()
        {
            print("TOGGLING ANIMATION");
            if (playing) { stopAnimation(); }
            else startAnimation();

        }


        public int getFrameLength(int trackOn, int frameOn)
        {

            CanvasScript canvasOn = timeline[trackOn].Frames[frameOn].canvas;
            (int, int) coord = getCanvasIndex(canvasOn);
            // coord.Item2 > track

            int frameLength = 0;
            while (
                coord.Item1 + frameLength < timeline[coord.Item2].Frames.Count &&
                timeline[coord.Item2].Frames[coord.Item1 + frameLength].canvas.Equals(canvasOn)
                )
            {

                frameLength++;
            }


            return frameLength;


        }

        public float getSmoothAnimationTime(Track trackOn)
        {

            CanvasScript canvasAnimating = trackOn.Frames[getFrameOn()].canvas;
            (int, int) coord = getCanvasIndex(canvasAnimating);
            // coord.Item2 > track

            int frameLength = 0;
            while (
                coord.Item1 + frameLength < timeline[coord.Item2].Frames.Count &&
                timeline[coord.Item2].Frames[coord.Item1 + frameLength].canvas.Equals(canvasAnimating)
                )
            {

                frameLength++;
            }

            Debug.Log("SMOOTH TIME " + frameOn + " " + coord.Item1 + " " + frameLength);

            return (frameOn - (float)coord.Item1) / (float)(frameLength);


        }

        public void updateLayerTransforms()
        {
            int frameInt = getFrameOn();



            // Update layer animation transforms 

            if (frameInt >= 0)
            {

                Debug.Log("LAYER TRANSFORMS ]");
                for (int t = 0; t < timeline.Count; t++)
                {

                    if (frameInt >= timeline[t].Frames.Count) {continue;};

                    Debug.Log("TRACK " + t + " "  + frameInt);
                    Debug.Log(timeline[t].Frames[frameInt].animatedPath);
                  
                    
                    if (
                        (timeline[t].Frames[frameInt].animatedPath != null && frameInt == 0) ||
                        (timeline[t].Frames[frameInt].animatedPath != null &&
                        timeline[t].Frames[frameInt - 1].animatedPath != null)
                     // && !timeline[0].Frames[frameInt].animatedPath.Equals(timeline[0].Frames[frameInt - 1].animatedPath

                     )
                    {


                   
                        float canvasTime =  getSmoothAnimationTime(timeline[t]) * (timeline[t].Frames[frameInt].animatedPath.Path.NumPositionKnots - 1);
                        // Debug.Log("CANVAS TIME " + canvasTime + " | " + getSmoothAnimationTime(timeline[t]) + " | " + (timeline[0].Frames[frameInt].animatedPath.Path.NumPositionKnots));



                        TiltBrush.PathT pathTime = new TiltBrush.PathT( canvasTime);
                        TiltBrush.PathT pathStart = new TiltBrush.PathT(0);





                        // if (m_CurrentPathWidget.Path.RotationKnots.Count > 0)
                        // {
                        //     transform.rotation = m_CurrentPathWidget.Path.GetRotation(t);
                        // }
                        // float fov = m_CurrentPathWidget.Path.GetFov(t);
                        // SketchControlsScript.m_Instance.MovementPathCaptureRig.SetFov(fov);
                        // SketchControlsScript.m_Instance.MovementPathCaptureRig.UpdateCameraTransform(transform);

                        TrTransform pathPosition = TrTransform.FromTransform(timeline[t].Frames[frameInt].animatedPath.gameObject.transform);
                        
              
                       
                        TrTransform posStart =  App.Scene.Pose.inverse * TrTransform.TR(timeline[t].Frames[frameInt].animatedPath.Path.GetPosition(pathStart),timeline[t].Frames[frameInt].animatedPath.Path.GetRotation(pathStart));
                        TrTransform posNow =  App.Scene.Pose.inverse * TrTransform.TR(timeline[t].Frames[frameInt].animatedPath.Path.GetPosition(pathTime),timeline[t].Frames[frameInt].animatedPath.Path.GetRotation(pathTime));

                        TrTransform posDifference = posNow * posStart.inverse ;


            
                        timeline[t].Frames[frameInt].canvas.LocalPose = posDifference;
                        // Debug.Log("POSITION AFTER " + timeline[0].Frames[frameInt].canvas.LocalPose.ToString());

                        TrTransform pathPositionConstant = (pathPosition);

                        timeline[t].Frames[frameInt].animatedPath.gameObject.transform.position = pathPositionConstant.translation;

                        timeline[t].Frames[frameInt].animatedPath.gameObject.transform.rotation = pathPositionConstant.rotation;
                        // timeline[0].Frames[frameInt].animatedPath.gameObject.transform.rotation = pathPosition.rotation; 
                        // timeline[0].Frames[frameInt].animatedPath.gameObject.transform.scale = pathPosition.scale;


                        // for(int i = 0; i < timeline[0].Frames[frameInt].canvas.gameObject.transform.GetChildCount(); i++)
                        // {

                        //     GameObject Go = timeline[0].Frames[frameInt].canvas.gameObject.transform.GetChild(i).gameObject;

                        //     if (Go.GetComponent<Batch>() != null || Go.GetComponent<ModelWidget>() != null){
                        //         Go.transform.localPosition = Position;
                        //     }



                        // }









                    }
                }
            }
        }

        // Update is called once per frame
        float prevFrameOn = 0;

        int previousCanvasBatches = 0;
        CanvasScript lastCanvas = null;
        void Update()
        {



            if (lastCanvas != App.Scene.ActiveCanvas)
            {
                previousCanvasBatches = 0;
            }
            lastCanvas = App.Scene.ActiveCanvas;
            ;

            int currentBatchPools = App.Scene.ActiveCanvas.BatchManager.GetNumBatchPools();

            if (currentBatchPools != 0 && previousCanvasBatches != currentBatchPools)
            {


                resetTimeline();
                previousCanvasBatches = currentBatchPools;

            }

            if (playing)
            {
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                current = (time - start);
                frameOn = (((float)current) / (1000f / ((float)fps)));

                frameOn = frameOn % getTimelineLength();


                prevFrameOn = frameOn;






                int frameInt = getFrameOn();

                focusFrame(getFrameOn());



                // Update layer animation transforms 
                updateLayerTransforms();

            }
        }
    }
}
