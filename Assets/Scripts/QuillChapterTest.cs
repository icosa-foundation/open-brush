using UnityEngine;
using TiltBrush;
public class QuillChapterTest : MonoBehaviour { void Start() { string path = @"C:\Users\andyb\Documents\Quill\Chapters"; int count = Quill.GetQuillChapterCount(path); Debug.Log($"[TEST] Chapter count for {path}: {count}"); } }
