using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LineDrawing : MonoBehaviour
{
    private List<GameObject> _lines = new List<GameObject>();
    private LineRenderer _currentLine;
    private List<float> _currentLineWidths = new List<float>(); //list to store line widths

    [SerializeField] float _maxLineWidth = 0.01f;
    [SerializeField] float _minLineWidth = 0.0005f;

    [SerializeField] Material _material;

    [SerializeField] private Color _currentColor;
    public Color CurrentColor
    {
        get { return _currentColor; }
        set
        {
            _currentColor = value;
        }
    }

    public float MaxLineWidth
    {
        get { return _maxLineWidth; }
        set { _maxLineWidth = value; }
    }

    private bool _lineWidthIsFixed = false;
    public bool LineWidthIsFixed
    {
        get { return _lineWidthIsFixed; }
        set { _lineWidthIsFixed = value; }
    }

    private bool _isDrawing = false;
    private bool _doubleTapDetected = false;

    [SerializeField]
    private float longPressDuration = 1.0f;
    private float buttonPressedTimestamp = 0;

    [SerializeField]
    private StylusHandler _stylusHandler;

    private Vector3 _previousLinePoint;
    private const float _minDistanceBetweenLinePoints = 0.0005f;

    private void StartNewLine()
    {
        var gameObject = new GameObject("line");
        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
        _currentLine = lineRenderer;
        _currentLine.positionCount = 0;
        _currentLine.material = _material;
        _currentLine.material.color = _currentColor;
        _currentLine.loop = false;
        _currentLine.startWidth = _minLineWidth;
        _currentLine.endWidth = _minLineWidth;
        _currentLine.useWorldSpace = true;
        _currentLine.widthCurve = new AnimationCurve();
        _currentLineWidths = new List<float>();
        _currentLine.shadowCastingMode = ShadowCastingMode.Off;
        _currentLine.receiveShadows = false;
        _lines.Add(gameObject);
        _previousLinePoint = new Vector3(0, 0, 0);
    }

    private void AddPoint(Vector3 position, float width)
    {
        if (Vector3.Distance(position, _previousLinePoint) > _minDistanceBetweenLinePoints)
        {
            _previousLinePoint = position;
            _currentLine.positionCount++;
            _currentLineWidths.Add(Math.Max(width * _maxLineWidth, _minLineWidth));
            _currentLine.SetPosition(_currentLine.positionCount - 1, position);

            //create a new AnimationCurve
            AnimationCurve curve = new AnimationCurve();

            //populate the curve with keyframes based on the widths list
            if (_currentLineWidths.Count > 1)
            {
                for (int i = 0; i < _currentLineWidths.Count; i++)
                {
                    curve.AddKey(i / (float)(_currentLineWidths.Count - 1), _currentLineWidths[i]);
                }
            }
            else
            {
                curve.AddKey(0, _currentLineWidths[0]);
            }

            //assign the curve to the widthCurve
            _currentLine.widthCurve = curve;
        }
    }

    private void RemoveLastLine()
    {
        GameObject lastLine = _lines[_lines.Count - 1];
        _lines.RemoveAt(_lines.Count - 1);

        Destroy(lastLine);
    }

    private void ClearAllLines()
    {
        foreach (var line in _lines)
        {
            Destroy(line);
        }
        _lines.Clear();
    }

    void Update()
    {

        float analogInput = Mathf.Max(_stylusHandler.CurrentState.tip_value, _stylusHandler.CurrentState.cluster_middle_value);

        if (analogInput > 0 && _stylusHandler.CanDraw())
        {
            if (!_isDrawing)
            {
                StartNewLine();
                _isDrawing = true;
            }
            AddPoint(_stylusHandler.CurrentState.inkingPose.position, _lineWidthIsFixed ? 1.0f : analogInput);
        }
        else
        {
            _isDrawing = false;
        }

        //Undo by double tapping or clicking on cluster_back button on stylus
        if (_stylusHandler.CurrentState.cluster_back_double_tap_value ||
        _stylusHandler.CurrentState.cluster_back_value)
        {
            if (_lines.Count > 0 && !_doubleTapDetected)
            {
                buttonPressedTimestamp = Time.time;
                RemoveLastLine();
            }
            _doubleTapDetected = true;
            if (_lines.Count > 0 && Time.time >= (buttonPressedTimestamp + longPressDuration))
            {
                ClearAllLines();
            }
        }
        else
        {
            _doubleTapDetected = false;
        }
    }
}
