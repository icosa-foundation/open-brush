using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.Linq;

namespace TiltBrush.Animation{
    public class AnimationUI_Manager : MonoBehaviour
    {
       

       
      
        int fps = 5;
        
        float frameOn = 0f;

        public int getFrameOn(){
            return Math.Clamp((int)frameOn,0,timeline.Count-1) ;
        }
        long start = 0,current = 0, time = 0;

        bool playing = false;
        
        
        public struct frameLayer{
            public bool visible;
            public bool deleted;

            public CanvasScript canvas;
        }

        frameLayer newFrameLayer(CanvasScript canvas){
            frameLayer thisframeLayer;
            thisframeLayer.canvas = canvas;
            thisframeLayer.visible = (bool)App.Scene.IsLayerVisible(canvas);
            thisframeLayer.deleted = false;
            return thisframeLayer;
        }
        public struct Frame {
            public bool visible;
            public bool deleted;
            public List<frameLayer> layers;

        }
        Frame newFrame(){
            Frame thisFrame;
            thisFrame.layers = new List<frameLayer>();
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


        // Visual size of frame on timeline
        float sliderFrameSize = 0.12f;

        float timelineOffset = 0.0f;
        public List<GameObject> timelineNotches;

        public List<GameObject> timelineFrameObjects;



        public List<Frame> timeline;

        List<BatchPool> tempPool1;
        List<BatchPool> tempPool2;

        int batchIndex = 0;




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
               timeline = new List<Frame>();

            Frame originFrame = newFrame();
            frameLayer mainLayer = newFrameLayer(App.Scene.m_MainCanvas);

            originFrame.layers.Add(mainLayer);

            timeline.Add(originFrame);

            App.Scene.animationUI_manager = this;

            focusFrame(originFrame);

            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();

            resetTimeline();

            print("START TIMELINE");
        }
        public  void init(){
           
           print("INIT");
        }
       

        private void hideFrame(Frame frameHiding){
            frameHiding.visible = false;

            foreach(frameLayer layer in frameHiding.layers){
                App.Scene.HideLayer(layer.canvas);
            }

        }

        private void showFrame(Frame frameShowing){
            frameShowing.visible = true;

            foreach(frameLayer layer in frameShowing.layers){
                if (layer.visible && !layer.deleted) { 
                    App.Scene.ShowLayer(layer.canvas);
                }else{
                    App.Scene.HideLayer(layer.canvas);
                }
            }

        }

        public void AddLayerRefresh(CanvasScript canvasAdding){


            int numLayers = App.Scene.m_LayerCanvases.Count;
            int created = 0;
            print("THIS TIMELINE," + timeline);
             print("THIS TIMELINE COUNT," + timeline.Count);
            for (int i =0 ; i < timeline.Count; i++){

                if (i == frameOn){

                    frameLayer addingLayer = newFrameLayer(canvasAdding);
                    timeline[i].layers.Add(addingLayer);
                    created ++;
                    print("CREATED,"+ created);

                }else{
                    CanvasScript newCanvas = App.Scene.AddCanvas();
                    frameLayer addingLayer = newFrameLayer(newCanvas);
                    timeline[i].layers.Add(addingLayer);
                    created ++;
                    print("CREATED,"+ created);
                }
                

            }

        }

        public (int,int) getCanvasIndex(CanvasScript canvas){

            for (int i =0 ; i < timeline.Count; i++){

                for (int l =0 ; l < timeline[i].layers.Count; l++){

                    if (canvas.Equals(timeline[i].layers[l].canvas)){
                        return (i,l);
                    };

                 }

            }
            return (-1,-1);
        }

        public void printTimeline(){
            String timelineString = "";
            
             for (int i=0;i<timeline.Count;i++){
                timelineString += " Time-" + i + " " ;


                for (int l=0;l<timeline[i].layers.Count;l++){

                    timelineString += "[Frame " + timeline[i].layers[l].deleted + "] ";
                }

                timelineString += "\n";
             }
             print(timelineString);



        }

        public void updateLayerVisibilityRefresh(CanvasScript canvas){

            bool visible = canvas.gameObject.activeSelf;

            (int,int) canvasIndex = getCanvasIndex(canvas);

             if (canvasIndex.Item1 != -1){

                for (int i=0;i<timeline.Count;i++){

          

                    frameLayer changingLayer = timeline[i].layers[canvasIndex.Item2];
                    changingLayer.visible = visible;
                    // App.Scene.HideLayer(changingLayer.canvas);

                    timeline[i].layers[ canvasIndex.Item2] = changingLayer;
                 
                } 
            }

        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas){

            (int,int) canvasIndex = getCanvasIndex(canvas);

            print(" DELETING LAYER TRACK " + canvasIndex.Item2);

            if (canvasIndex.Item1 != -1){

                for (int i=0;i<timeline.Count;i++){

            
                    

                    frameLayer deletingLayer = timeline[i].layers[ canvasIndex.Item2];
                    deletingLayer.deleted = true;
                    App.Scene.HideLayer(deletingLayer.canvas);

                    timeline[i].layers[ canvasIndex.Item2] = deletingLayer;
                 
                } 
            }
        }


        public void SquashLayerRefresh(CanvasScript SquashedLayer, CanvasScript DestinationLayer){

            // (int,int) canvasIndex = getCanvasIndex(canvas);

            // print(" DELETING LAYER TRACK " + canvasIndex.Item2);


            (int,int) SquashedCoord = getCanvasIndex(SquashedLayer);
            (int,int) DestinationCoord = getCanvasIndex(DestinationLayer);

            Stroke[] m_OriginalStrokes;

            if (SquashedCoord.Item1 != -1 && DestinationCoord.Item1 != -1){

                for (int i=0;i<timeline.Count;i++){

                    if (i != frameOn){
     
                        m_OriginalStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas == timeline[i].layers[SquashedCoord.Item2].canvas).ToArray();
                
                        foreach (var stroke in m_OriginalStrokes){

                            stroke.SetParentKeepWorldPosition(timeline[i].layers[DestinationCoord.Item2].canvas);

                        }
                    }

                    frameLayer squashingLayer = timeline[i].layers[SquashedCoord.Item2];
                    squashingLayer.deleted = true;
             
                    timeline[i].layers[SquashedCoord.Item2] = squashingLayer;

                    App.Scene.HideLayer( timeline[i].layers[SquashedCoord.Item2].canvas);
           
                 
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

 
            for (int f = 0; f < timeline.Count; f++){
         

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
                for(int i = 0; i < timeline[f].layers.Count; i++)
                {
                    numDeleted += timeline[f].layers[i].deleted ? 1 : 0;
                    
                    int layerOn = i - numDeleted;

                    if (layerOn < timeline[f].layers.Count && !timeline[f].layers[i].deleted){
                        var frameButton =  frameWrapper.transform.GetChild(layerOn);
                        frameButton.gameObject.SetActive(true);
                        frameButton.gameObject.GetComponent<FrameButton>().setButtonCoordinate(i,f);

                        print("NUM BATCH POOLS: " + timeline[f].layers[i].canvas.BatchManager.GetNumBatchPools());

                        bool filled = timeline[f].layers[i].canvas.BatchManager.GetNumBatchPools() > 0;

                        frameButton.GetChild(0).gameObject.SetActive(filled);
                        frameButton.GetChild(1).gameObject.SetActive(!filled);

              
                    }
              
                }
 

            }
            updateTimelineSlider();

        }

        public void updateTimelineSlider(){

            float meshLength = timelineRef.GetComponent<TimelineSlider>().m_MeshScale.x;
            float startX = -meshLength/2f - timelineOffset*meshLength;
         

              for (int f = 0; f < timeline.Count; f++){

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

        public void selectTimelineFrame(int layerNum,int frameNum){


            print("SELECT TIMELINE FRAME " + layerNum + " " + frameNum);
            App.Scene.ActiveCanvas = timeline[frameNum].layers[layerNum].canvas;
            frameOn = frameNum;
            focusFrame(timeline[frameNum]);

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

            float max = sliderFrameSize*(float)timeline.Count - 1;
            timelineOffset = Math.Clamp(timelineOffset,0,  max < 0 ? 0 : max );

            float clampedval = (float)newVal;
            clampedval = Math.Clamp(clampedval,0,1 );

            timelineRef.GetComponent<TimelineSlider>().setSliderValue(clampedval);
        }
        public void updateFrameInfo(){
            textRef.GetComponent<TextMeshPro>().text = (frameOn.ToString("0.00")) + ":" + timeline.Count;
        }
        public void updateUI(bool timelineInput = false){
            updateFrameInfo();
            updateTimelineSlider();
            if (!timelineInput) updateTimelineNob();

            deleteFrameButton.SetActive(frameOn != 0);
    
        }

        private void focusFrame(Frame frame, bool timelineInput = false){

   
    
            for (int i=0;i<timeline.Count;i++){

                if (timeline[i].Equals(frame)) {
                    // frameOn = i;
                    continue;
                }
                print("HIDING IN FOCUS FRAME");
                hideFrame(timeline[i]);

            }
 

            for (int i = 0; i< frame.layers.Count;i++){

                if (i ==0) { 
                    App.Scene.m_MainCanvas = frame.layers[i].canvas;
                    continue;
                }

                print("INFO " + i + " " +  App.Scene.m_LayerCanvases.Count + " " + frame.layers.Count);

                App.Scene.m_LayerCanvases[i - 1] = frame.layers[i].canvas;

            }




            (int,int ) previousActiveCanvas = getCanvasIndex(App.Scene.ActiveCanvas);

            print("PREV CANV INDEX " + previousActiveCanvas.Item1 + " " + previousActiveCanvas.Item2);
 
            if (previousActiveCanvas.Item2 != -1){
                App.Scene.ActiveCanvas = frame.layers[previousActiveCanvas.Item2].canvas;
            }

     
            showFrame(frame);

            updateUI(timelineInput);
          
        }
        public void removeKeyFrame(){

            if (frameOn <= 0) return;

            print("BEFORE REMOVE");
            printTimeline();

            int previousLayerActive = getCanvasIndex(App.Scene.ActiveCanvas).Item2;
 

            for (int l =0;l< timeline[getFrameOn()].layers.Count; l++){

         
    
                //App.Scene.destroyCanvas(timeline[frameOn].layers[l].canvas);
                App.Scene.HideCanvas(timeline[getFrameOn()].layers[l].canvas);
            }

            Frame removingFrame = timeline[getFrameOn()];
            removingFrame.deleted = true;

            timeline.RemoveAt(getFrameOn());

            frameOn = Math.Clamp(frameOn,0,timeline.Count - 1);

            print("AFTER REMOVE");
            printTimeline();

            App.Scene.ActiveCanvas = timeline[getFrameOn()].layers[previousLayerActive].canvas;
            focusFrame(timeline[getFrameOn()]);

            resetTimeline();

        }
        public void addKeyFrame(){
            
            Frame addingFrame = newFrame();

            for (int l =0;l< timeline[0].layers.Count; l++){

         
                CanvasScript newCanvas = App.Scene.AddCanvas();
                frameLayer addingLayer = newFrameLayer(newCanvas);
                addingLayer.deleted = timeline[0].layers[l].deleted;
                addingFrame.layers.Add(addingLayer);
                print("ADDING LAYER");
            
            }
  

            print("ADDING FRAME NUM LAYERS -" + addingFrame.layers.Count);
            ;  
            timeline.Insert(getFrameOn() + 1,addingFrame);

            focusFrame(addingFrame);   
            
            print("TIMELINE SIZE -" + timeline.Count);

      
            resetTimeline();


        
        }

        
        public void duplicateKeyFrame(){
            
            Frame addingFrame = newFrame();
            print("DUPLICATE NOW");
            printTimeline();

            for (int l =0;l< timeline[getFrameOn()].layers.Count; l++){

         
                CanvasScript newCanvas = App.Scene.AddCanvas();

            

                  

                    List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas 
                            ==
                             timeline[getFrameOn()].layers[l].canvas
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

                
                 
          


                frameLayer addingLayer = newFrameLayer(newCanvas);
                addingLayer.deleted = timeline[getFrameOn()].layers[l].deleted;
                addingFrame.layers.Add(addingLayer);
                print("ADDING LAYER");
            
            }
  

            print("ADDING FRAME NUM LAYERS -" + addingFrame.layers.Count);
            ;  
            timeline.Insert(getFrameOn() + 1,addingFrame);

            focusFrame(addingFrame);   
            
            print("TIMELINE SIZE -" + timeline.Count);

            resetTimeline();

        
        }
        
      
      
        public void timelineSlide(float Value){
           
            frameOn =  ((float)(Value + timelineOffset) / sliderFrameSize );



            frameOn = frameOn >= timeline.Count ? timeline.Count  : frameOn;
            frameOn = frameOn < 0 ? 0 : frameOn;
            
            print("T SLIDE frameoN- " + frameOn);
            focusFrame( timeline[getFrameOn()],true);

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
            float max = sliderFrameSize*(float)timeline.Count - 1;
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

                frameOn = frameOn % timeline.Count;

                if (frameOn - prevFrameOn > 1){
                    print("DIFFERENCE " + frameOn + " "  + prevFrameOn +" " + current + " " + (1000f / ((float)fps)));
                }

                prevFrameOn = frameOn;

                print("FRAME ON - " + frameOn + " " + current);

              

            

                
                focusFrame( timeline[getFrameOn()]);
           

               
            }
        }
    }
}
