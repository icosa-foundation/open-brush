using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.Linq;

namespace TiltBrush.FrameAnimation{
    public class AnimationUI_Manager : MonoBehaviour
    {
       

       
      
        int fps = 8;
        
        float frameOn = 0f;

        public int getFrameOn(){
            return Math.Clamp((int)frameOn,0,timeline[0].Frames.Count-1) ;
        }
        long start = 0,current = 0, time = 0;

        bool playing = false;
        
        
        // public struct frameLayer{
        //     public bool visible;
        //     public bool deleted;

        //     public CanvasScript canvas;
        // }

        public struct Frame {
            public bool visible;
            public bool deleted;
            public CanvasScript canvas;

        }

        public struct Track{
            public List<Frame> Frames;
            public bool visible;
            public bool deleted;

        }

        
        public Frame newFrame(CanvasScript canvas){
            Frame thisframeLayer;
            thisframeLayer.canvas = canvas;
            thisframeLayer.visible = (bool)App.Scene.IsLayerVisible(canvas);
            thisframeLayer.deleted = false;
            return thisframeLayer;
        }
        
        Track newTrack(){
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

        [SerializeField] public GameObject layersPanel;

        bool animationMode = true;


        // Visual size of frame on timeline
        float sliderFrameSize = 0.12f;

        float timelineOffset = 0.0f;
        public List<GameObject> timelineNotches;

        public List<GameObject> timelineFrameObjects;

        public List<Track> timeline;




        // List<CanvasScript> Frames = new List<CanvasScript>();

         // Start is called before the first frame update
        void Start()
        {
      
            // nextFrame = App.Scene.addFrame();

            print("START ANIM");
        }
        void Awake(){

            
            App.Scene.animationUI_manager = this;
          
           
        }
        public void startTimeline(){
            timeline = new List<Track>();

            Track mainTrack = newTrack();
            Frame originFrame = newFrame(App.Scene.m_MainCanvas);


            mainTrack.Frames.Add(originFrame);

            timeline.Add(mainTrack);

            App.Scene.animationUI_manager = this;

            focusFrame(0);

            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();

            resetTimeline();

            print("START TIMELINE");
        }
        public  void init(){
           
           print("INIT");
        }
       

        private void hideFrame(int frameIndex){
            
            foreach(Track track in timeline){
                Frame thisFrame =  track.Frames[frameIndex];

                App.Scene.HideLayer( thisFrame.canvas);
                thisFrame.visible = false;
                track.Frames[frameIndex] = thisFrame;
            }

        }

        private void showFrame(int frameIndex){
            
            
           

            print("SHOWING FRAME ++ " + frameIndex);

            foreach(Track track in timeline){
                Frame thisFrame =  track.Frames[frameIndex];

                thisFrame.visible = true;

                 print("THIS FRAME NOW  ++ " + thisFrame.visible + " " + thisFrame.deleted);
                
                if (track.visible && !thisFrame.deleted) { 
                    print("SHOWING HERE ++ ");
                    App.Scene.ShowLayer(thisFrame.canvas);
                    thisFrame.visible = true;
                }else{
                    print("HIDING HERE ++ ");
                    App.Scene.HideLayer(thisFrame.canvas);
                    thisFrame.visible = false;
                }
                 track.Frames[frameIndex] = thisFrame;
            }
       

        }

        public void AddLayerRefresh(CanvasScript canvasAdding){


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
            

            for (int i =0 ; i < timeline.Count; i++){

                if (i == frameOn){

                    Frame addingFrame = newFrame(canvasAdding);
                    addingTrack.Frames.Add(addingFrame);
             

                }else{
                    Frame addingFrame = newFrame(App.Scene.AddCanvas());
                    addingTrack.Frames.Add(addingFrame);
                  
          
                }
                

            }
            timeline.Add(addingTrack);


        }

        public (int,int) getCanvasIndex(CanvasScript canvas){

            for (int trackNum =0 ; trackNum < timeline.Count; trackNum++){

                for (int frameNum =0 ; frameNum < timeline[trackNum].Frames.Count; frameNum++){

                    if (canvas.Equals(timeline[trackNum].Frames[frameNum].canvas)){
                        return (frameNum,trackNum);
                    };

                 }

            }
            return (-1,-1);
        }

    

        public CanvasScript getTimelineCanvas(int frameIndex, int trackIndex ){
     
            if (timeline.Count > frameIndex){
                   if (timeline[trackIndex].Frames.Count > frameIndex){ 
                    return timeline[trackIndex].Frames[frameIndex].canvas;
                   }
            }
           return App.Scene.MainCanvas;
            

        }

        public List<List<CanvasScript>> getTrackCanvases()
        {
             List<List<CanvasScript>> timelineCavses = new List<List<CanvasScript>>();

             for (int l=0;l<timeline[0].Frames.Count;l++){
                    List<CanvasScript> canvasFrames = new List<CanvasScript>();

                    for (int i=0;i<timeline.Count;i++){

                        canvasFrames.Add(timeline[i].Frames[l].canvas);
               };

                timelineCavses.Add(canvasFrames);
             }


            return timelineCavses;
        }


        public void printTimeline(){
            String timelineString = "";
            
             for (int i=0;i<timeline.Count;i++){
                timelineString += " Track-" + i + " " ;


                for (int l=0;l<timeline[i].Frames.Count;l++){

                    timelineString += "[Frame " + timeline[i].Frames[l].deleted + "] ";
                }

                timelineString += "\n";
             }
             print(timelineString);



        }

        public void updateLayerVisibilityRefresh(CanvasScript canvas){

            bool visible = canvas.gameObject.activeSelf;

            (int,int) canvasIndex = getCanvasIndex(canvas);

             if (canvasIndex.Item1 != -1){

    
                Track thisTrack = timeline[canvasIndex.Item2];
                thisTrack.visible = visible;
               

                for (int i=0;i<thisTrack.Frames.Count;i++){

          

                    Frame changingFrame = thisTrack.Frames[i];
                    changingFrame.visible = visible;
                    // App.Scene.HideLayer(changingLayer.canvas);

                    thisTrack.Frames[i] = changingFrame;
                 
                } 

                 timeline[canvasIndex.Item2] = thisTrack;
            }

        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas){

            (int,int) canvasIndex = getCanvasIndex(canvas);

            print(" DELETING LAYER TRACK " + canvasIndex.Item2);

            if (canvasIndex.Item1 != -1){

                Track thisTrack = timeline[canvasIndex.Item2];
                thisTrack.deleted = true;

                for (int i=0;i<thisTrack.Frames.Count;i++){

            
                    
                    
                    Frame deletingFrame = thisTrack.Frames[i];
                    deletingFrame.deleted = true;
                    App.Scene.HideLayer(deletingFrame.canvas);

                    thisTrack.Frames[i] = deletingFrame;
                 
                } 
                timeline[canvasIndex.Item2] = thisTrack;
            }
        }


        public void SquashLayerRefresh(CanvasScript SquashedLayer, CanvasScript DestinationLayer){

            // (int,int) canvasIndex = getCanvasIndex(canvas);

            // print(" DELETING LAYER TRACK " + canvasIndex.Item2);


            (int,int) SquashedCoord = getCanvasIndex(SquashedLayer);
            (int,int) DestinationCoord = getCanvasIndex(DestinationLayer);

            Stroke[] m_OriginalStrokes;

            if (SquashedCoord.Item1 != -1 && DestinationCoord.Item1 != -1){

                for (int i=0;i<timeline[0].Frames.Count;i++){

                    if (i != frameOn){
     
                        m_OriginalStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas == timeline[SquashedCoord.Item2].Frames[i].canvas).ToArray();
                
                        foreach (var stroke in m_OriginalStrokes){

                            stroke.SetParentKeepWorldPosition(timeline[DestinationCoord.Item2].Frames[i].canvas);

                        }
                    }

                    Frame squashingFrame = timeline[SquashedCoord.Item2].Frames[i];
                    squashingFrame.deleted = true;
             
                    timeline[SquashedCoord.Item2].Frames[i] = squashingFrame;

                    App.Scene.HideLayer( timeline[SquashedCoord.Item2].Frames[i].canvas);
           
                 
                } 
            }
        }

        public void DestroyLayerRefresh(CanvasScript canvasAdding){



        }


        public void resetTimeline(){

                  
            print("RESET TIMELINE");
            print(timelineNotches);
      

            if (timelineNotches != null){
                foreach (var notch in timelineNotches){
                        Destroy(notch);

                }
            }
            if (timelineFrameObjects != null){
                foreach (var frame in timelineFrameObjects){
                        Destroy(frame);

                }
            }
            foreach (Transform thisObj in timelineField.transform){
                GameObject.Destroy(thisObj.gameObject);
            }
            
            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();

 
            for (int f = 0; f < timeline[0].Frames.Count; f++){
         

                GameObject newNotch =  Instantiate(timelineNotchPrefab);

                newNotch.transform.FindChild("Num").GetComponent<TextMeshPro>().text = "" + f;

                newNotch.transform.SetParent(timelineRef.transform);
         
        
                newNotch.SetActive(false);

                timelineNotches.Add(newNotch);

                



                GameObject newFrame =  Instantiate(timelineFramePrefab,timelineField.transform,false);



                // newFrame.transform.SetParent(timelineField.transform);
                timelineFrameObjects.Add(newFrame);

                newFrame.name = "FrameContainer_" + f.ToString();


                GameObject frameWrapper = newFrame.transform.GetChild(0).gameObject;

                int numDeleted = 0;

                for(int i = 0; i < frameWrapper.transform.childCount; i++)
                {
                    frameWrapper.transform.GetChild(i).gameObject.SetActive(false);
                }

                for(int i = 0; i < timeline.Count; i++)
                {
                    numDeleted += timeline[i].Frames[f].deleted ? 1 : 0;
                    
                    int layerOn = i - numDeleted;

                    if (layerOn < timeline.Count && !timeline[i].Frames[f].deleted){
                        var frameButton =  frameWrapper.transform.GetChild(layerOn);
                        frameButton.gameObject.SetActive(true);
                        frameButton.gameObject.GetComponent<FrameButton>().setButtonCoordinate(i,f);

                        print("NUM BATCH POOLS: " + timeline[i].Frames[f].canvas.BatchManager.GetNumBatchPools());

                        bool filled = timeline[i].Frames[f].canvas.BatchManager.GetNumBatchPools() > 0;

                        frameButton.GetChild(0).gameObject.SetActive(filled);
                        frameButton.GetChild(1).gameObject.SetActive(!filled);

              
                    }
              
                }
 

            }
            updateTimelineSlider();
            updateTimelineNob();

        }

        public void updateTimelineSlider(){

            float meshLength = timelineRef.GetComponent<TimelineSlider>().m_MeshScale.x;
            float startX = -meshLength/2f - timelineOffset*meshLength;
         

              for (int f = 0; f < timeline[0].Frames.Count; f++){

                        float thisOffset = ((float)(f))*sliderFrameSize*meshLength;
                     
                        float notchOffset = startX + ((float)(f))*sliderFrameSize*meshLength;
                        if(timelineNotches.ElementAtOrDefault(f) != null)
                        {
                        // logic
                        
                        GameObject notch = timelineNotches[f];
                 

                      
                           notch.transform.localPosition = new Vector3(notchOffset, 0, 0);
                        notch.transform.localRotation = Quaternion.identity;
                 

                        notch.SetActive(notchOffset >= -meshLength*0.5 && notchOffset <=  meshLength*0.5);
                        }

                        if (timelineFrameObjects.ElementAtOrDefault(f) != null){
                            Vector3 newPosition = timelineFrameObjects[f].transform.localPosition ;
                            float width = timelineFrameObjects[f].transform.GetChild(0).localScale.x;
                          
                            newPosition.x = thisOffset - timelineOffset*meshLength - width*0.5f;

                        timelineFrameObjects[f].transform.localPosition = new Vector3(newPosition.x, 0, 0);;
                        timelineFrameObjects[f].transform.localRotation = Quaternion.identity;
                        timelineFrameObjects[f].SetActive(newPosition.x >= -0.1 && newPosition.x <=  meshLength - width);
                        }

            }

                  
        }

        public void selectTimelineFrame(int trackNum,int frameNum){


            print("SELECT TIMELINE FRAME " + trackNum + " " + frameNum);
            App.Scene.ActiveCanvas = timeline[trackNum].Frames[frameNum].canvas;
            frameOn = frameNum;
            focusFrame(frameNum);

            resetTimeline();
            updateTimelineNob();
        }
        public void updateTimelineNob(){

            float newVal =  (float)(frameOn-0.01)*sliderFrameSize - timelineOffset;


        
            if (newVal >= 0.9f){
                timelineOffset += newVal - 0.9f;
              
                 print ("SCROLL RIGHT " +  timelineOffset);
            }
            if (newVal <= 0.1f){
                 timelineOffset += newVal - 0.1f;
                 print ("SCROLL RIGHT " +  timelineOffset);
            }

            float max = sliderFrameSize*(float)timeline[0].Frames.Count - 1;
            timelineOffset = Math.Clamp(timelineOffset,0,  max < 0 ? 0 : max );

            float clampedval = (float)newVal;
            clampedval = Math.Clamp(clampedval,0,1 );

            timelineRef.GetComponent<TimelineSlider>().setSliderValue(clampedval);
        }
        public void updateFrameInfo(){
            textRef.GetComponent<TextMeshPro>().text = (frameOn.ToString("0.00")) + ":" + timeline[0].Frames.Count;
        }
        public void updateUI(bool timelineInput = false){
            updateFrameInfo();
            updateTimelineSlider();
            if (!timelineInput) updateTimelineNob();

            deleteFrameButton.SetActive(frameOn != 0);
    
        }

        public void focusFrameNum(int frameNum){
            focusFrame(frameNum);
        }

        private void focusFrame(int FrameIndex, bool timelineInput = false){

   
    
            for (int i=0;i<timeline[0].Frames.Count;i++){

                if (i == FrameIndex) {
                    // frameOn = i;
                    continue;
                }
                print("HIDING IN FOCUS FRAME");
                hideFrame(i);

            }
 


            // App.Scene.m_LayerCanvases = new List<CanvasScript>(new CanvasScript[frame.layers.Count]);
            for (int i = 0; i< timeline.Count;i++){

                if (i ==0) { 
                    App.Scene.m_MainCanvas = timeline[i].Frames[FrameIndex].canvas;
                    continue;
                }

                print("INFO " + i + " " +  App.Scene.m_LayerCanvases.Count + " ");

                App.Scene.m_LayerCanvases[i - 1] = timeline[i].Frames[FrameIndex].canvas;

            }




            (int,int ) previousActiveCanvas = getCanvasIndex(App.Scene.ActiveCanvas);

            print("PREV CANV INDEX " + previousActiveCanvas.Item1 + " " + previousActiveCanvas.Item2);
 
            if (previousActiveCanvas.Item2 != -1){
                App.Scene.ActiveCanvas = timeline[previousActiveCanvas.Item2].Frames[FrameIndex].canvas;
            }

     
            showFrame(FrameIndex);

            updateUI(timelineInput  );
          
        }
        public void removeKeyFrame(){

            if (frameOn <= 0) return;

            print("BEFORE REMOVE");
            printTimeline();

            int previousTrackActive = getCanvasIndex(App.Scene.ActiveCanvas).Item2;
 

            for (int l =0;l< timeline.Count; l++){

         
    
                //App.Scene.destroyCanvas(timeline[frameOn].layers[l].canvas);
                App.Scene.HideCanvas(timeline[l].Frames[getFrameOn()].canvas);

                Frame removingFrame = timeline[l].Frames[getFrameOn()];
                removingFrame.deleted = true;

                timeline[l].Frames.RemoveAt(getFrameOn());
            }

            

            frameOn = Math.Clamp(frameOn,0,timeline.Count - 1);

            print("AFTER REMOVE");
            printTimeline();

            App.Scene.ActiveCanvas = timeline[previousTrackActive].Frames[getFrameOn()].canvas;
            focusFrame(getFrameOn());

            resetTimeline();

        }
        public void clearTimeline(){
            timeline = new List<Track>();
        }
         public void addLayersRaw(String name, bool visible,bool mainTrack = false){

            
            Track addingTrack = newTrack();

            for (int i =0 ; i < timeline[0].Frames.Count; i++){

             
                if (mainTrack && i ==0){
                  
                    App.Scene.MainCanvas.gameObject.SetActive(visible);
                    Frame addingFrame = newFrame(App.Scene.MainCanvas);
                    addingTrack.Frames.Add(addingFrame);
                    
                }else{
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
            
             print("BEFORE  ADD");

            for (int l =0;l< timeline.Count; l++){
                
                print("ADDING LAYER HERE - " + l);
                Frame addingFrame = newFrame(App.Scene.AddCanvas());
                // CanvasScript newCanvas = App.Scene.AddCanvas();
                // frameLayer addingLayer = newFrameLayer(newCanvas);
                addingFrame.deleted = timeline[l].Frames[0].deleted;
                print("ADDING LAYER - " + l);
              
                timeline[l].Frames.Insert(getFrameOn() + 1,addingFrame);
            
            }
  

            ;  
            
            frameOn++;
            focusFrame((int)frameOn);   
            
            print("TIMELINE SIZE -" + timeline[0].Frames.Count);

      
            resetTimeline();


        
        }

        
        public void duplicateKeyFrame(){
            
            

            for (int l =0;l< timeline.Count; l++){

          
             

            
                    CanvasScript newCanvas = App.Scene.AddCanvas();
                  

                    List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas 
                            ==
                             timeline[l].Frames[getFrameOn()].canvas
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
                print("DUPLICATE NOW");
                printTimeline();

                addingFrame.deleted = timeline[l].Frames[getFrameOn()].deleted;


                timeline[l].Frames.Insert(getFrameOn() + 1,addingFrame);
                // frameLayer addingLayer = newFrameLayer(newCanvas);
                // addingLayer.deleted = timeline[getFrameOn()].layers[l].deleted;
                // addingFrame.layers.Add(addingLayer);
                print("ADDING LAYER");
            
            }
  

          

            focusFrame((int)frameOn+1);   
            


            resetTimeline();

        
        }
        
      
      
        public void timelineSlide(float Value){
           
            frameOn =  ((float)(Value + timelineOffset) / sliderFrameSize );



            frameOn = frameOn >= timeline[0].Frames.Count ? timeline[0].Frames.Count  : frameOn;
            frameOn = frameOn < 0 ? 0 : frameOn;
            
            print("T SLIDE frameoN- " + frameOn);
            focusFrame(getFrameOn(),true);

            // Scrolling the timeline
            print ("TIMELINE SCROLLING " +  Value);
            if (Value < 0.1f){
                timelineOffset -= 0.05f;
                print ("SCROLL LEFT " +  timelineOffset);
            }
            if (Value > 0.9f){
                timelineOffset += 0.05f;
                 print ("SCROLL RIGHT " +  timelineOffset);
            }
            float max = sliderFrameSize*(float)timeline[0].Frames.Count - 1;
            timelineOffset = Math.Clamp(timelineOffset,0,  max < 0 ? 0 : max );


            updateTimelineSlider();
        }

        public void startAnimation(){
            start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            playing = true;
            // App.Scene.LayerCanvases
            // App.Scene.ShowLayer();
        }
        public void stopAnimation(){
            playing = false;
        }
        public void toggleAnimation(){
            print("TOGGLING ANIMATION");
            if (playing) { stopAnimation();}
            else startAnimation();
      
        }



    

        // Update is called once per frame
        float prevFrameOn = 0;

        int previousCanvasBatches = 0;
        CanvasScript lastCanvas = null;
        void Update()
        {

            print("UPDATE TIMELINE: ");
            printTimeline();
            if (lastCanvas != App.Scene.ActiveCanvas){
                previousCanvasBatches = 0;
            }
            lastCanvas = App.Scene.ActiveCanvas;
            ;

            int currentBatchPools = App.Scene.ActiveCanvas.BatchManager.GetNumBatchPools();
            
            if (currentBatchPools != 0 && previousCanvasBatches != currentBatchPools ){


                resetTimeline();
                previousCanvasBatches = currentBatchPools;

            }
            print("ANIM UPDATE");
            if (playing){
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                current = (time - start);
                frameOn = (((float)current) / (1000f / ((float)fps))) ;

                frameOn = frameOn % timeline[0].Frames.Count;

                if (frameOn - prevFrameOn > 1){
                    print("DIFFERENCE " + frameOn + " "  + prevFrameOn +" " + current + " " + (1000f / ((float)fps)));
                }

                prevFrameOn = frameOn;

                print("FRAME ON - " + frameOn + " " + current);

              

            

                
                focusFrame( getFrameOn());
           

               
            }
        }
    }
}
