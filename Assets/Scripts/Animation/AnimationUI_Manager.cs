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
        bool scrolling = false;





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

        public struct DeletedFrame{
            public Frame frame;
            public int length;
            public (int,int) location;
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



            if (animationPathCanvas == null ){
                animationPathCanvas = new GameObject("AnimationPaths");
                animationPathCanvas.transform.parent = App.Scene.gameObject.transform;
                animationPathCanvas.AddComponent<CanvasScript>();
            }
      


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


                App.Scene.HideCanvas(thisFrame.canvas);
                thisFrame.visible = false;
                track.Frames[hidingFrame] = thisFrame;
            }


            // App.Scene.triggerLayersUpdate();
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
            
                    App.Scene.ShowCanvas(thisFrame.canvas);
                    thisFrame.visible = true;
                }
                else
                {
                 
                    App.Scene.HideCanvas(thisFrame.canvas);
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

        public bool getFrameFilled(int track,int frame){
            return timeline[track].Frames[frame].canvas.gameObject.transform.childCount > 0;
        }
        public bool getFrameFilled(CanvasScript canvas){
            (int,int) loc = getCanvasLocation(canvas);
            return timeline[loc.Item1].Frames[loc.Item2].canvas.gameObject.transform.childCount > 0;
        }


        public void addAnimationPath(MovementPathWidget pathwidget,int trackNum,int frameNum)
        {

  
    
            
            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(animationPathCanvas.transform);

            pathwidget.setPathAnimation(true);


            (int, int) Loc = (trackNum,frameNum);
            pathwidget.Path.timelineLocation = Loc;

         

            Debug.Log("ADDING ANIMATINON PATH! " + Loc.Item1 + " " + Loc.Item2);



            CanvasScript origCanvas = timeline[Loc.Item1].Frames[Loc.Item2].canvas;

            if (timeline[Loc.Item1].Frames[Loc.Item2].animatedPath != null)
            {
                TiltBrush.WidgetManager.m_Instance.DeleteMovementPath(timeline[Loc.Item1].Frames[Loc.Item2].animatedPath);
            }

            int i = 0;

            List<Frame> framesChanging = new List<Frame>();
            while (
                Loc.Item2 + i < timeline[Loc.Item1].Frames.Count &&
                timeline[Loc.Item1].Frames[Loc.Item2 + i].canvas.Equals(origCanvas)
                )
            {

                i++;
            }



            for (int c = 0; c < i; c++)
            {

                Frame changingFrame = timeline[Loc.Item1].Frames[Loc.Item2 + c];

                changingFrame.animatedPath = pathwidget;
                timeline[Loc.Item1].Frames[Loc.Item2 + c] = changingFrame;
            }


            Debug.Log("FINISHED ANIMATED PATH ");
            Debug.Log(timeline[Loc.Item1].Frames[Loc.Item2].animatedPath);





        }
   
        public void addAnimationPath(MovementPathWidget pathwidget)
        {

  
    
            
            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(animationPathCanvas.transform);

            pathwidget.setPathAnimation(true);


            (int, int) Loc = getCanvasLocation(App.Scene.ActiveCanvas);
            pathwidget.Path.timelineLocation = Loc;

            if (!getFrameFilled(Loc.Item1,Loc.Item2)){
                    TiltBrush.WidgetManager.m_Instance.UnregisterGrabWidget(pathwidget.gameObject);
                    Destroy(pathwidget);
                    return;
            }

            Debug.Log("ADDING ANIMATINON PATH! " + Loc.Item1 + " " + Loc.Item2);



            CanvasScript origCanvas = timeline[Loc.Item1].Frames[Loc.Item2].canvas;

            if (timeline[Loc.Item1].Frames[Loc.Item2].animatedPath != null)
            {
                TiltBrush.WidgetManager.m_Instance.DeleteMovementPath(timeline[Loc.Item1].Frames[Loc.Item2].animatedPath);
            }

            int i = 0;

            List<Frame> framesChanging = new List<Frame>();
            while (
                Loc.Item2 + i < timeline[Loc.Item1].Frames.Count &&
                timeline[Loc.Item1].Frames[Loc.Item2 + i].canvas.Equals(origCanvas)
                )
            {

                i++;
            }



            for (int c = 0; c < i; c++)
            {

                Frame changingFrame = timeline[Loc.Item1].Frames[Loc.Item2 + c];

                changingFrame.animatedPath = pathwidget;
                timeline[Loc.Item1].Frames[Loc.Item2 + c] = changingFrame;
            }


            Debug.Log("FINISHED ANIMATED PATH ");
            Debug.Log(timeline[Loc.Item1].Frames[Loc.Item2].animatedPath);


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
               Frame addingFrame;
                if (i == getFrameOn()){
                 addingFrame = newFrame(canvasAdding);
                }
                else{
                 addingFrame = newFrame(App.Scene.AddCanvas());
                }
                
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
            resetTimeline();
        }


        
        // public (int, int) getCanvasIndex(CanvasScript canvas)
        // {

        //     for (int trackNum = 0; trackNum < timeline.Count; trackNum++)
        //     {

        //         for (int frameNum = 0; frameNum < timeline[trackNum].Frames.Count; frameNum++)
        //         {

        //             if (canvas.Equals(timeline[trackNum].Frames[frameNum].canvas))
        //             {
        //                 return (frameNum, trackNum);
        //             };

        //         }

        //     }
        //     return (-1, -1);
        // }


        public (int, int) getCanvasLocation(CanvasScript canvas)
        {

            for (int trackNum = 0; trackNum < timeline.Count; trackNum++)
            {

                for (int frameNum = 0; frameNum < timeline[trackNum].Frames.Count; frameNum++)
                {

                    if (canvas.Equals(timeline[trackNum].Frames[frameNum].canvas))
                    {
                        return (trackNum, frameNum );
                    };

                }

            }
            return (-1, -1);
        }



        public CanvasScript getTimelineCanvas(int trackIndex, int frameIndex )
        {

            if (trackIndex < timeline.Count )
            {
                if (frameIndex < timeline[trackIndex].Frames.Count)
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

                    timelineString += "[ Frame D: " + (getFrameFilled(i,l)  ? "1" : "0") + " A:" + (timeline[i].Frames[l].animatedPath != null  ? "1" : "0") + " ] ";
                }

                timelineString += "\n";
            }
            print(timelineString);



        }

        public void updateLayerVisibilityRefresh(CanvasScript canvas)
        {

            bool visible = canvas.gameObject.activeSelf;

            (int, int) canvasIndex = getCanvasLocation(canvas);

            if (canvasIndex.Item2 != -1)
            {


                Track thisTrack = timeline[canvasIndex.Item1];
                thisTrack.visible = visible;


                for (int i = 0; i < thisTrack.Frames.Count; i++)
                {



                    Frame changingFrame = thisTrack.Frames[i];
                    changingFrame.visible = visible;
                    // App.Scene.HideLayer(changingLayer.canvas);

                    thisTrack.Frames[i] = changingFrame;

                }

                timeline[canvasIndex.Item1] = thisTrack;
            }

        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas)
        {

            (int, int) canvasIndex = getCanvasLocation(canvas);

            print(" DELETING LAYER TRACK " + canvasIndex.Item1);

            if (canvasIndex.Item2 != -1)
            {

                Track thisTrack = timeline[canvasIndex.Item1];
                thisTrack.deleted = true;
                

                // for (int i = 0; i < thisTrack.Frames.Count; i++)
                // {




                //     Frame deletingFrame = thisTrack.Frames[i];
                //     deletingFrame.deleted = true;
                //     App.Scene.HideLayer(deletingFrame.canvas);

                //     thisTrack.Frames[i] = deletingFrame;

                // }
                timeline[canvasIndex.Item1] = thisTrack;
            }
            resetTimeline();
        }


        public void SquashLayerRefresh(CanvasScript SquashedLayer, CanvasScript DestinationLayer)
        {


            (int, int) SquashedCoord = getCanvasLocation(SquashedLayer);
            (int, int) DestinationCoord = getCanvasLocation(DestinationLayer);

            Stroke[] m_OriginalStrokes;

            if (SquashedCoord.Item2 != -1 && DestinationCoord.Item2 != -1)
            {

                for (int i = 0; i < getTimelineLength(); i++)
                {

                 
                        
                    if (i != frameOn)
                    {

                        m_OriginalStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas == timeline[SquashedCoord.Item1].Frames[i].canvas).ToArray();

                        foreach (var stroke in m_OriginalStrokes)
                        {

                            stroke.SetParentKeepWorldPosition(timeline[DestinationCoord.Item1].Frames[i].canvas);

                        }
                    }

                    Frame squashingFrame = timeline[SquashedCoord.Item1].Frames[i];
                    squashingFrame.deleted = true;

                    timeline[SquashedCoord.Item1].Frames[i] = squashingFrame;

                    App.Scene.HideLayer(timeline[SquashedCoord.Item1].Frames[i].canvas);


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


                  

                    numDeleted += timeline[i].deleted ? 1 : 0;

                    int trackOn = i - numDeleted;

                    if (trackOn < timeline.Count && !timeline[i].deleted)
                    {



                     

                        // var frameButton =  frameWrapper.transform.GetChild(layerOn);
                        var newButton = Instantiate(frameButtonPrefab, frameWrapper.transform, false);
                        var frameButton = newButton.transform.GetChild(0);

                        frameButton.GetComponent<MeshRenderer>().enabled = false;


                        // frameButton.localScale = new Vector3(1f,1f,7.88270617f);
                        frameButton.localPosition = new Vector3(0.00538007962f, 0.449999988f - frameOffset * trackOn, 0);
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

                                timeline[i].Frames[f].animatedPath.gameObject.SetActive(timeline[i].Frames[f].canvas.Equals(App.Scene.ActiveCanvas));
                            }

                        }
            
                     
                        bool filled = getFrameFilled(i,f);

                        bool backwardsConnect = false ,forwardConnect = false;

                        // if (filled){
                        backwardsConnect = (f > 0 && timeline[i].Frames[f].canvas.Equals(timeline[i].Frames[f - 1].canvas));
                        forwardConnect = (f < timeline[i].Frames.Count - 1 && timeline[i].Frames[f].canvas.Equals(timeline[i].Frames[f + 1].canvas));
                        // }



                        frameButton.GetChild(Convert.ToInt32(filled)).gameObject.SetActive(true);

                        int backBox = 6;
                        frameButton.GetChild(backBox).gameObject.SetActive(true);


                        // Set behind colours depending on if frame is active
                        Color backColor;
                        if (filled){
                                if (timeline[i].Frames[f].canvas.Equals(App.Scene.ActiveCanvas) ){
                                    backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                                }else{
                                    backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f);
                                }
                        }else{

                            (int,int) index = getCanvasLocation(App.Scene.ActiveCanvas);
                            if (index.Item1 == i && f == getFrameOn() ){
                                    backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                            }else{
                                    backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f);
                            }
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




            updateTimelineSlider();
            updateTimelineNob();
            updateTrackScroll();
            updateUI();

            App.Scene.triggerLayersUpdate();

            
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
            for (int f = 0; f < timelineLength; f++)
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
            frameOn = Math.Clamp((int)frameNum, 0, timeline[trackNum].Frames.Count - 1);;
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
                App.Scene.ActiveCanvas != null && App.Scene.ActiveCanvas != timeline[0].Frames[0].canvas &&
                getFrameFilled(App.Scene.ActiveCanvas)
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




            (int, int) previousActiveCanvas = getCanvasLocation(App.Scene.ActiveCanvas);

            print("PREV CANV INDEX " + previousActiveCanvas.Item1 + " " + previousActiveCanvas.Item2);

            if (previousActiveCanvas.Item1 != -1 && FrameIndex < timeline[previousActiveCanvas.Item1].Frames.Count)
            {
                App.Scene.ActiveCanvas = timeline[previousActiveCanvas.Item1].Frames[FrameIndex].canvas;
            }


            showFrame(FrameIndex);

            // Debug.Log("SHOWING FRAME " + FrameIndex);
            // Debug.Log(timeline[0].Frames[FrameIndex].canvas);

            updateUI(timelineInput);
            
            App.Scene.triggerLayersUpdate();

        }
        public DeletedFrame removeKeyFrame(int trackNum = -1, int frameNum = -1)
        {



            print("BEFORE REMOVE");
            printTimeline();

            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  
            (int,int) nextIndex = getFollowingFrameIndex(index.Item1,index.Item2);



            DeletedFrame deletedFrame;

            deletedFrame.frame = timeline[index.Item1].Frames[index.Item2];
            deletedFrame.frame.canvas = timeline[index.Item1].Frames[index.Item2].canvas;
            deletedFrame.length = getFrameLength(index.Item1,index.Item2);
            deletedFrame.location = (index.Item1,index.Item2);
     

            // App.Scene.DestroyCanvas(timeline[index.Item1].Frames[index.Item2].canvas);

            App.Scene.HideCanvas(timeline[index.Item1].Frames[index.Item2].canvas);
            CanvasScript replacingCanvas = App.Scene.AddCanvas();
            for (int l = index.Item2; l < nextIndex.Item2; l++)
            {



            

                Frame removingFrame =  newFrame(replacingCanvas);
                // removingFrame.deleted = true;

                timeline[index.Item1].Frames[l] = removingFrame;



            }

     

            

            print("REMOVE TIMELINE PRINT");
            printTimeline();


            fillandCleanTimeline();


  

            selectTimelineFrame(index.Item1, Math.Clamp(index.Item2, 0, getTimelineLength() - 1));
       
   
            resetTimeline();



            return deletedFrame;


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

        
        public (int,int) getFollowingFrameIndex(int trackNum, int frameNum) {

            int frameAt = frameNum;
            while (frameAt <  timeline[trackNum].Frames.Count){

            if ( !timeline[trackNum].Frames[frameAt].canvas.Equals(timeline[trackNum].Frames[frameNum].canvas)){
                return (trackNum,frameAt);
            }

            frameAt++;
            
            }

            return (trackNum,frameAt);


        }



        public int getTimelineMaxCanvas(){
            int maxLength = 0;

            for (int t = 0; t < timeline.Count; t++){
                for (int f = 0; f < timeline[t].Frames.Count; f++){
                    if (f > maxLength && getFrameFilled(t,f)){

                        maxLength = f;

                    }
                }
        

            }
            return maxLength;
        }


        public void cleanTimeline(){

            int maxTimeline = getTimelineMaxCanvas();

            List<Track> newTimeline = new List<Track>();

            for (int t = 0; t < timeline.Count; t++){

                Track addingTrack = newTrack();
                addingTrack.deleted = timeline[t].deleted;
                newTimeline.Add(addingTrack);
                int f;
                for (f = 0; f < timeline[t].Frames.Count; f++){
                   
                   if (f > maxTimeline){

                   }else{   
                    newTimeline[t].Frames.Add(timeline[t].Frames[f]);
                   }


                }


        

            }
          
            
            timeline = newTimeline;


            
            // focusFrame(getFrameOn());
            // resetTimeline();
            

        }

         public void fillTimeline(){

            int maxTimeline = getTimelineLength();

            List<Track> newTimeline = new List<Track>();

            for (int t = 0; t < timeline.Count; t++){

                Track addingTrack = newTrack();
                addingTrack.deleted = timeline[t].deleted;
                newTimeline.Add(addingTrack);
                int f;
            
                for (f = 0; f < timeline[t].Frames.Count; f++){
                   
 
                    newTimeline[t].Frames.Add(timeline[t].Frames[f]);
                   

                }

                if (f < maxTimeline){
                    while ( f < maxTimeline){

                        Frame  addingFrame = newFrame(App.Scene.AddCanvas());

                        newTimeline[t].Frames.Add(addingFrame); 

                        f++;


                    }
                }
        

            }
            
            timeline = newTimeline;


            

        }
        // Make sures there aren't too many or few empty frames
        public void fillandCleanTimeline(){
            fillTimeline();
            cleanTimeline();
        }
        public (int,int) moveKeyFrame(bool moveRight, int trackNum = -1, int frameNum = -1)
        {



            print("BEFORE REMOVE");
            printTimeline();

            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  

            (int, int ) nextIndex = getFollowingFrameIndex(index.Item1,index.Item2);
            bool failure = false;


        
            if (moveRight){
                if (nextIndex.Item2 >= timeline[nextIndex.Item1].Frames.Count ){

                    Frame  emptyFrame = newFrame(App.Scene.AddCanvas());


                    
            
                    Frame  movedFrame = timeline[index.Item1].Frames[index.Item2];


                    timeline[index.Item1].Frames[index.Item2] = emptyFrame;
                    timeline[nextIndex.Item1].Frames.Insert(timeline[nextIndex.Item1].Frames.Count, movedFrame);

                }else if ( !getFrameFilled(nextIndex.Item1,nextIndex.Item2)){

                    Frame tempFrame =  timeline[nextIndex.Item1].Frames[nextIndex.Item2 ];

                    timeline[nextIndex.Item1].Frames[nextIndex.Item2 ] = timeline[index.Item1].Frames[index.Item2];

                    timeline[index.Item1].Frames[index.Item2] = tempFrame;


                }else{
                    failure = true;  
                }
            }else{


                if (index.Item2 > 0  &&   !getFrameFilled(index.Item1,index.Item2 - 1)){

                    int frameLength = getFrameLength(index.Item1,index.Item2);

                    Frame tempFrame =  timeline[index.Item1].Frames[index.Item2-1 ];


                    timeline[index.Item1].Frames[index.Item2 - 1 ] = timeline[index.Item1].Frames[index.Item2 + frameLength - 1];

                    timeline[index.Item1].Frames[index.Item2 + frameLength - 1] = tempFrame;

                }else{
                    failure = true;  
                }

            }

            if (failure) return (-1,-1);


            fillandCleanTimeline();
            

            if (moveRight) { 

                selectTimelineFrame(nextIndex.Item1,nextIndex.Item2); 
                return (index.Item1,index.Item2 + 1);

            }else{

                 selectTimelineFrame(index.Item1,index.Item2 - 1); 
                 return (index.Item1,index.Item2 - 1);
            }

          
        }
        // For loading the scene
            public void addKeyFrame(int trackNum){

            (int, int) index = (trackNum, timeline[trackNum].Frames.Count-1);
                (int, int ) nextIndex = getFollowingFrameIndex(index.Item1,index.Item2);

                if (nextIndex.Item2 >= timeline[nextIndex.Item1].Frames.Count ){

                    Frame  addingFrame = newFrame(App.Scene.AddCanvas());

                    timeline[nextIndex.Item1].Frames.Insert(timeline[nextIndex.Item1].Frames.Count, addingFrame);
                    nextIndex.Item2 = timeline[nextIndex.Item1].Frames.Count - 1;

                }else if(  getFrameFilled(nextIndex.Item1,nextIndex.Item2)) {

                    Frame  addingFrame = newFrame(App.Scene.AddCanvas());

                    timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);



                }

        

        }

        public (int,int) addKeyFrame(int trackNum = -1, int frameNum = -1)
        {



            print("BEFORE REMOVE");
            printTimeline();

            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  


            Debug.Log("ON REDO");
            (int, int ) insertingAt;
            (int, int ) nextIndex = getFollowingFrameIndex(index.Item1,index.Item2);

            if (nextIndex.Item2 >= timeline[nextIndex.Item1].Frames.Count ){

                  AnimationUI_Manager.Frame addingFrame = newFrame(App.Scene.AddCanvas());

                timeline[nextIndex.Item1].Frames.Insert(timeline[nextIndex.Item1].Frames.Count, addingFrame);
                nextIndex.Item2 = timeline[nextIndex.Item1].Frames.Count - 1;


          
                insertingAt = (nextIndex.Item1,timeline[nextIndex.Item1].Frames.Count - 1);
       

            }else if(  getFrameFilled(nextIndex.Item1,nextIndex.Item2)) {

                AnimationUI_Manager.Frame  addingFrame = newFrame(App.Scene.AddCanvas());

                timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);


                insertingAt = nextIndex;
         


            }else{
                     insertingAt = nextIndex;
         
            }

            fillTimeline();

            selectTimelineFrame(nextIndex.Item1,nextIndex.Item2);

            return insertingAt;
            
        }


         public void extendKeyFrame(int trackNum)  {

            

            print("BEFORE  ADD");

            (int, int) index = (trackNum, timeline[trackNum].Frames.Count-1);


           
                    
            Frame  addingFrame = newFrame(timeline[index.Item1].Frames[index.Item2].canvas);
                addingFrame.deleted = timeline[index.Item1].Frames[index.Item2].deleted;
                addingFrame.animatedPath = timeline[index.Item1].Frames[index.Item2].animatedPath;

            timeline[index.Item1].Frames.Insert(index.Item2 + 1, addingFrame);
                            

                    

                                //     if (filled){
                                        
                                //        addingFrame = newFrame(App.Scene.AddCanvas());
                                //     }else{

                                //           addingFrame = newFrame(timeline[l].Frames[timeline[l].Frames.Count - 1].canvas);
                                //             addingFrame.deleted = timeline[l].Frames[timeline[l].Frames.Count - 1].deleted;
                                //             addingFrame.animatedPath = timeline[l].Frames[timeline[l].Frames.Count - 1].animatedPath;
                                //     }

                                //           print("ADDING LAYER - " + l);       

                                //      timeline[l].Frames.Insert(timeline[l].Frames.Count, addingFrame);

                                // }
                    

                            
                        
                                

            

                   
       
            ;

           



        }


        public (int,int) extendKeyFrame(int trackNum = -1, int frameNum = -1)
        {



            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  

     

            // print("ADDING LAYER HERE - " + index.Item2);
            //             // Frame addingFrame = newFrame(App.Scene.AddCanvas());

            // Frame addingFrame = newFrame(timeline[index.Item2].Frames[index.Item1].canvas);
            //             // CanvasScript newCanvas = App.Scene.AddCanvas();
            //             // frameLayer addingLayer = newFrameLayer(newCanvas);
            // addingFrame.deleted = timeline[index.Item2].Frames[0].deleted;
            // addingFrame.animatedPath = timeline[index.Item2].Frames[index.Item1].animatedPath;
            // print("ADDING LAYER - " + index.Item1);

            // timeline[index.Item2].Frames.Insert(index.Item1 + 1, addingFrame);


        
            
            if ( !getFrameFilled(index.Item1,index.Item2)) {return (-1,-1);}


            int frameLength = getFrameLength(index.Item1,index.Item2);


            if (   index.Item2 + frameLength >= timeline[index.Item1].Frames.Count || getFrameFilled(index.Item1,index.Item2 + frameLength) ){

                    for (int l = 0; l < timeline.Count; l++)
                    {

                        


                                if (l == index.Item1){

                                    Frame  addingFrame = newFrame(timeline[l].Frames[index.Item2].canvas);
                                        addingFrame.deleted = timeline[l].Frames[index.Item2].deleted;
                                        addingFrame.animatedPath = timeline[l].Frames[index.Item2].animatedPath;

                                    timeline[l].Frames.Insert(index.Item2 + 1, addingFrame);
                                }else{


                                print("ADDING LAYER HERE - " + l);
                                Frame addingFrame = newFrame(App.Scene.AddCanvas());
                                timeline[l].Frames.Insert(timeline[l].Frames.Count, addingFrame);
                                // addingFrame = newFrame(App.Scene.AddCanvas());
                                }
                                // Frame addingFrame;

                    

                                //     if (filled){
                                        
                                //        addingFrame = newFrame(App.Scene.AddCanvas());
                                //     }else{

                                //           addingFrame = newFrame(timeline[l].Frames[timeline[l].Frames.Count - 1].canvas);
                                //             addingFrame.deleted = timeline[l].Frames[timeline[l].Frames.Count - 1].deleted;
                                //             addingFrame.animatedPath = timeline[l].Frames[timeline[l].Frames.Count - 1].animatedPath;
                                //     }

                                //           print("ADDING LAYER - " + l);       

                                //      timeline[l].Frames.Insert(timeline[l].Frames.Count, addingFrame);

                                // }

                            
                        
                                

            }

                   
            }else{

            
   

                     Frame  addingFrame = newFrame(timeline[index.Item1].Frames[index.Item2].canvas);
                                addingFrame.deleted = timeline[index.Item1].Frames[index.Item2].deleted;
                                addingFrame.animatedPath = timeline[index.Item1].Frames[index.Item2].animatedPath;

                            timeline[index.Item1].Frames[index.Item2 + frameLength ] = addingFrame;



            }
            ;

            frameOn++;
            focusFrame((int)frameOn);

            print("TIMELINE SIZE -" + getTimelineLength());


            resetTimeline();
            return index;


        }

         public (int,int) reduceKeyFrame(int trackNum = -1, int frameNum = -1)
        {



            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  



                int frameLength = getFrameLength(index.Item1,index.Item2);

                if (frameLength > 1){
                    Frame emptyFrame = newFrame(App.Scene.AddCanvas());
                        timeline[index.Item1].Frames[index.Item2 + frameLength -1] = emptyFrame;
                      
                        frameOn--;
                        focusFrame(getFrameOn());
                        resetTimeline();
                }   


            fillandCleanTimeline();
            return index;

        }

        public (int,int) splitKeyFrame(int trackNum = -1, int frameNum = -1)
        {


            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  


            CanvasScript newCanvas = App.Scene.AddCanvas();       
            CanvasScript oldCanvas = App.Scene.ActiveCanvas;

            int frameLegnth = getFrameLength(index.Item1,index.Item2 );


            int splittingIndex = getFrameOn() ;
            if (splittingIndex < index.Item2  || splittingIndex > index.Item2 +frameLegnth - 1   ) return (-1,-1);




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


           

            for (int f = splittingIndex ; f < index.Item2 + frameLegnth; f++){

                Frame addingFrame = newFrame(newCanvas);
                timeline[index.Item1].Frames[f] = addingFrame;
            }

            selectTimelineFrame(index.Item1,splittingIndex);
            resetTimeline();

            return (index.Item1,splittingIndex);
        }
        public  (int, int) duplicateKeyFrame(int trackNum = -1, int frameNum = -1)
        {



            print("BEFORE REMOVE");
            printTimeline();

            (int,int) index = (trackNum == -1 || frameNum == -1) ? getCanvasLocation(App.Scene.ActiveCanvas) : (trackNum,frameNum);  

            // (int, int) index = getCanvasLocation(App.Scene.ActiveCanvas);

            CanvasScript newCanvas = App.Scene.AddCanvas();       
            CanvasScript oldCanvas = App.Scene.ActiveCanvas;

            int frameLegnth = getFrameLength(index.Item1,index.Item2 );
            (int, int) nextIndex = getFollowingFrameIndex(index.Item1,index.Item2 );






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


           

            for (int f = 0 ; f < frameLegnth; f++){

                     

                    if ( nextIndex.Item2 + f < timeline[nextIndex.Item1].Frames.Count && !getFrameFilled(nextIndex.Item1,nextIndex.Item2)  ){
                        Destroy(timeline[nextIndex.Item1].Frames[nextIndex.Item2 + f].canvas);

                        Frame addingFrame = newFrame(newCanvas);       

                        timeline[nextIndex.Item1].Frames[nextIndex.Item2 + f] = addingFrame ; 
                    }else{
                        Frame addingFrame = newFrame(newCanvas);      
                        timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2 + f,addingFrame)  ;                
                    }
                 
            }



            // timeline[canvasCoord.Item2].Frames[canvasCoord.Item1 + 1] = addingFrame;

            // int i = 2;
            // while (canvasCoord.Item1 + i < timeline[canvasCoord.Item2].Frames.Count &&
            //  timeline[canvasCoord.Item2].Frames[canvasCoord.Item1 + i].canvas.Equals(oldCanvas)
            //  )
            // {
            //     timeline[canvasCoord.Item2].Frames[canvasCoord.Item1 + i] = newFrame(newCanvas);
            //     i++;
            // }


            fillTimeline();
            selectTimelineFrame(nextIndex.Item1,nextIndex.Item2);
            resetTimeline();


           
            return nextIndex;

        }

        public void timelineSlideDown(bool down ){
   
            scrolling = down;
            
        }

        public void timelineSlide(float Value)
        {

            this.gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().OnDisable();
            

            frameOn = ((float)(Value + timelineOffset) / sliderFrameSize);


            int timelineLength = getTimelineLength();
            frameOn = frameOn >= timelineLength ? timelineLength : frameOn;
            frameOn = frameOn < 0 ? 0 : frameOn;

            // print("T SLIDE frameoN- " + frameOn);
            focusFrame(getFrameOn(), true);
            updateLayerTransforms();

            // Scrolling the timeline
            // print("TIMELINE SCROLLING " + Value);
            if (Value < 0.1f)
            {
                timelineOffset -= 0.05f;
                // print("SCROLL LEFT " + timelineOffset);
            }
            if (Value > 0.9f)
            {
                timelineOffset += 0.05f;
                // print("SCROLL RIGHT " + timelineOffset);
            }
            float max = sliderFrameSize * (float)timelineLength - 1;
            timelineOffset = Math.Clamp(timelineOffset, 0, max < 0 ? 0 : max);


            // updateTimelineSlider();

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
        public bool getChanging(){
            return playing || scrolling;
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
            (int, int) coord = getCanvasLocation(canvasOn);
            // coord.Item2 > track

            int frameLength = 0;
            while (
                coord.Item2 + frameLength < timeline[coord.Item1].Frames.Count &&
                timeline[coord.Item1].Frames[coord.Item2 + frameLength].canvas.Equals(canvasOn)
                )
            {

                frameLength++;
            }


            return frameLength;


        }

        public float getSmoothAnimationTime(Track trackOn)
        {

            CanvasScript canvasAnimating = trackOn.Frames[getFrameOn()].canvas;
            (int, int) coord = getCanvasLocation(canvasAnimating);
            // coord.Item2 > track

            int frameLength = 0;
            while (
                coord.Item2 + frameLength < timeline[coord.Item1].Frames.Count &&
                timeline[coord.Item1].Frames[coord.Item2 + frameLength].canvas.Equals(canvasAnimating)
                )
            {

                frameLength++;
            }

            // Debug.Log("SMOOTH TIME " + frameOn + " " + coord.Item2 + " " + frameLength);

            return (frameOn - (float)coord.Item2) / (float)(frameLength);


        }

        public void updateLayerTransforms()
        {
            int frameInt = getFrameOn();


            Debug.Log("BEFORE TIMELINE PRINT");
            printTimeline();

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
                        (timeline[t].Frames[frameInt].animatedPath != null)
                        // (timeline[t].Frames[frameInt].animatedPath != null && frameInt == 0) ||
                        // (timeline[t].Frames[frameInt].animatedPath != null &&
                        // timeline[t].Frames[frameInt - 1].animatedPath != null)
                     // && !timeline[0].Frames[frameInt].animatedPath.Equals(timeline[0].Frames[frameInt - 1].animatedPath

                     )
                    {

                        if (t == 1){

                        }
                   
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

            // (int,int) index = getCanvasLocation(App.Scene.ActiveCanvas);
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
