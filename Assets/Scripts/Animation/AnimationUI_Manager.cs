using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.Linq;

namespace TiltBrush.Animation{
    public class AnimationUI_Manager : MonoBehaviour
    {
       

       
      
        int fps = 5, frameOn = 0;
        long start = 0,current = 0 ;

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
        [SerializeField] public GameObject timelineMeshRef;
        [SerializeField] public GameObject timelineNotch;
        [SerializeField] public GameObject textRef;

        [SerializeField] public GameObject deleteFrameButton;



        public List<Frame> timeline;

        List<BatchPool> tempPool1;
        List<BatchPool> tempPool2;

        int batchIndex = 0;

        // Visual size of frame on timeline
        float frameTimelineSize = 0.2f;


        // List<CanvasScript> Frames = new List<CanvasScript>();

         // Start is called before the first frame update
        void Start()
        {
            tempPool1 = new List<BatchPool>();
            tempPool2 = new List<BatchPool>();

            // nextFrame = App.Scene.addFrame();

          
        }
        void Awake(){

            timeline = new List<Frame>();

            Frame originFrame = newFrame();
            frameLayer mainLayer = newFrameLayer(App.Scene.m_MainCanvas);

            originFrame.layers.Add(mainLayer);

            timeline.Add(originFrame);

            App.Scene.animationUI_manager = this;

            focusFrame(originFrame);
           
        }
        public  void init(){
           
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
                    CanvasScript newCanvas = App.Scene.addCanvas();
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



        public void updateTimelineSlider(){
       

                  
              
                    // GameObject notchTemp = timelineNotch;

                    // GameObject Clone = Instantiate(timelineNotch, new Vector3(0, 0, 0), Quaternion.identity);
                    // Clone.SetActive(true);

                    // Clone.transform.SetParent(timelineRef.transform);

                    // print(" CLONE OBJECT " + Clone);
                    // Debug.Log(Clone);
            
                    // timelineRef.GetComponent<TimelineSlider>().setSliderScale( (float)frameTimelineSize*timeline.Count);
                    timelineRef.GetComponent<TimelineSlider>().setSliderValue( (float)frameOn/timeline.Count);
        }
        public void updateFrameInfo(){
            textRef.GetComponent<TextMeshPro>().text = (frameOn+1) + ":" + timeline.Count;
        }
        public void updateUI(){
            updateFrameInfo();
            updateTimelineSlider();

            deleteFrameButton.SetActive(frameOn != 0);
    
        }

        private void focusFrame(Frame frame){

   
    
            for (int i=0;i<timeline.Count;i++){

                if (timeline[i].Equals(frame)) {
                    frameOn = i;
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
 
            App.Scene.ActiveCanvas = frame.layers[previousActiveCanvas.Item2].canvas;

     
            showFrame(frame);

            updateUI();
          
        }
        public void removeKeyFrame(){

            if (frameOn <= 0) return;

            print("BEFORE REMOVE");
            printTimeline();

            int previousLayerActive = getCanvasIndex(App.Scene.ActiveCanvas).Item2;
 

            for (int l =0;l< timeline[frameOn].layers.Count; l++){

         
    
                //App.Scene.destroyCanvas(timeline[frameOn].layers[l].canvas);
                App.Scene.HideCanvas(timeline[frameOn].layers[l].canvas);
            }

            Frame removingFrame = timeline[frameOn];
            removingFrame.deleted = true;

            timeline.RemoveAt(frameOn);

            frameOn = Math.Clamp(frameOn,0,timeline.Count - 1);

            print("AFTER REMOVE");
            printTimeline();

            App.Scene.ActiveCanvas = timeline[frameOn].layers[previousLayerActive].canvas;
            focusFrame(timeline[frameOn]);

        }
        public void addKeyFrame(){
            
            Frame addingFrame = newFrame();

            for (int l =0;l< timeline[0].layers.Count; l++){

         
                CanvasScript newCanvas = App.Scene.addCanvas();
                frameLayer addingLayer = newFrameLayer(newCanvas);
                addingLayer.deleted = timeline[0].layers[l].deleted;
                addingFrame.layers.Add(addingLayer);
                print("ADDING LAYER");
            
            }
  

            print("ADDING FRAME NUM LAYERS -" + addingFrame.layers.Count);
            ;  
            timeline.Insert(frameOn + 1,addingFrame);

            focusFrame(addingFrame);   
            
            print("TIMELINE SIZE -" + timeline.Count);

            
            float meshLength = timelineRef.GetComponent<TimelineSlider>().m_Mesh.transform.localScale.x;

            GameObject newNotch =  Instantiate(timelineNotch, new Vector3(0,0,0), Quaternion.identity);

            newNotch.transform.SetParent(timelineRef.transform);
            newNotch.transform.SetLocalPositionAndRotation(new Vector3(-meshLength/2f,0,0),Quaternion.identity);
            
       
            newNotch.SetActive(true);


            GameObject endNotch =  Instantiate(timelineNotch, new Vector3(0,0,0), Quaternion.identity);

            endNotch.transform.SetParent(timelineRef.transform);
            endNotch.transform.SetLocalPositionAndRotation(new Vector3(meshLength/2f,0,0),Quaternion.identity);

            endNotch.SetActive(true);




        
        }

        
        public void duplicateKeyFrame(){
            
            Frame addingFrame = newFrame();
            print("DUPLICATE NOW");
            printTimeline();

            for (int l =0;l< timeline[frameOn].layers.Count; l++){

         
                CanvasScript newCanvas = App.Scene.addCanvas();

            

                  

                    List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                            .Where(x => x.Canvas 
                            ==
                             timeline[frameOn].layers[l].canvas
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
                addingLayer.deleted = timeline[frameOn].layers[l].deleted;
                addingFrame.layers.Add(addingLayer);
                print("ADDING LAYER");
            
            }
  

            print("ADDING FRAME NUM LAYERS -" + addingFrame.layers.Count);
            ;  
            timeline.Insert(frameOn + 1,addingFrame);

            focusFrame(addingFrame);   
            
            print("TIMELINE SIZE -" + timeline.Count);

          

        
        }
        
      
      
        public void timelineSlide(float Value){
            frameOn =   (int)(((float)timeline.Count)*Value);

            frameOn = frameOn >= timeline.Count ? timeline.Count - 1 : frameOn;
            frameOn = frameOn < 0 ? 0 : frameOn;
            
            print("T SLIDE frameoN- " + frameOn);
            focusFrame( timeline[frameOn]);
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
            if (playing) { stopAnimation();}
            else startAnimation();
      
        }

    

        // Update is called once per frame
        int prevFrameOn = 0;
        void Update()
        {
            if (playing){
                current = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                long offset = (current - start);
                frameOn = (int)(((float)offset) / (1000f / ((float)fps))) ;

                frameOn = frameOn % timeline.Count;

                if (frameOn - prevFrameOn > 1){
                    print("DIFFERENCE " + frameOn + " "  + prevFrameOn +" " + offset + " " + (1000f / ((float)fps)));
                }

                prevFrameOn = frameOn;

                print("FRAME ON - " + frameOn + " " + offset);

              

            

                
                focusFrame( timeline[frameOn]);
           

               
            }
        }
    }
}
