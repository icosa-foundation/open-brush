# SVGMeshUnity

<img width="757" alt="typo" src="https://user-images.githubusercontent.com/1482297/36834202-765a8abe-1d75-11e8-8be9-369d5978ed71.png">

Generates mesh from [SVG path](https://github.com/beinteractive/SVGMeshUnity/blob/de472c2f716329e2991f75fcab9bd7253cc8ff12/Assets/Samples/Scripts/Simple.cs#L8-L14) in realtime for Unity.

This is a port of https://github.com/mattdesl/svg-mesh-3d

## Install

Copy `Assets/SVGMeshUnity` directory to your Assets directory.

Add a `SVGMesh` component to a GameObject that has `MeshFilter` and `MeshRenderer`.

<img width="321" alt="inspector" src="https://user-images.githubusercontent.com/1482297/36832680-d7e41b0c-1d6f-11e8-93f6-98b146e924dc.png">

## Usage

### Create mesh from SVG path

<img width="239" alt="twitter" src="https://user-images.githubusercontent.com/1482297/36832746-20568ff0-1d70-11e8-8cc1-7e8d0f2156b4.png"> <img width="252" alt="twtitter-wire" src="https://user-images.githubusercontent.com/1482297/36832554-49228084-1d6f-11e8-9049-7a5e9bdc7b1b.png">

```C#
var mesh = GetComponent<SVGMesh>();
var svg = new SVGData();
svg.Path("M17.316,6.246c0.008,0.162,0.011,... and so on");
mesh.Fill(svg);
```

Simply create an instance of `SVGData` and set SVG path data string by calling `SVGData.Path()`.
Then call `Mesh.Fill()`, a mesh will be generated.

### Create mesh from code

Instead of use SVG path data, you can directly make path data in your code.

<img width="239" src="https://user-images.githubusercontent.com/1482297/36834678-f834db56-1d76-11e8-8845-56547b673004.gif">

```C#
void Update()
{
    SVG.Clear();

    var resolution = 5;
    var radius = 3f;
    
    SVG.Move(NoisedR(radius, 0), 0f);
    
    for (var i = 0; i < resolution; ++i)
    {
        var i0 = i;
        var i1 = (i + 1) % resolution;
        
        var angle0 = Mathf.PI * 2f * ((float) i0 / resolution);
        var angle1 = Mathf.PI * 2f * ((float) i1 / resolution);

        var r0 = NoisedR(radius, i0);
        var r1 = NoisedR(radius, i1);
        var x0 = Mathf.Cos(angle0) * r0;
        var y0 = Mathf.Sin(angle0) * r0;
        var x1 = Mathf.Cos(angle1) * r1;
        var y1 = Mathf.Sin(angle1) * r1;

        var cx = x0 + (x1 - x0) * 0.5f;
        var cy = y0 + (y1 - y0) * 0.5f;
        var ca = Mathf.Atan2(cy, cx);
        var cr = 0.3f + (Mathf.PerlinNoise(Time.time, i * -100f) - 0.5f) * 1.15f;
        cx += Mathf.Cos(ca) * cr;
        cy += Mathf.Sin(ca) * cr;
        
        SVG.Curve(cx, cy, cx, cy, x1, y1);
    }
    
    Mesh.Fill(SVG);
}

private float NoisedR(float r, float randomize)
{
    return r + (Mathf.PerlinNoise(Time.time, randomize * 10f) - 0.5f) * 0.5f;
}
```

<img width="239" src="https://user-images.githubusercontent.com/1482297/37012499-4b776d64-2138-11e8-856a-1241272288c6.gif">

```C#
public class Move : MonoBehaviour
{
    [SerializeField] private float R = 0.5f;
    [SerializeField] private float IntervalRate = 1f;
    
    [SerializeField] private SVGMesh HeadMesh;
    [SerializeField] private SVGMesh TailMesh;
    [SerializeField] private SVGMesh BodyMesh;

    private SVGData HeadSVG;
    private SVGData TailSVG;
    private SVGData BodySVG;

    private Vector2 Head;
    private Vector2 Tail;

    private Vector2 To;

    private float HeadR;
    private float TailR;

    private float Interval;
    private float FollowTime;

    void Start()
    {
        HeadSVG = new SVGData();
        TailSVG = new SVGData();
        BodySVG = new SVGData();

        Head = RandomField();
        Tail = Head;
    }

    void Update()
    {
        HeadSVG.Clear();
        TailSVG.Clear();
        BodySVG.Clear();

        Update(Time.deltaTime);

        var v = Mathf.Max(0.7f, 1.3f - Mathf.Clamp01((Head - Tail).magnitude / 3f));
        
        Circle(HeadSVG, Head, HeadR);
        Circle(TailSVG, Tail, TailR);
        Metaball(BodySVG, Head, HeadR, Tail, TailR, v);
        
        HeadMesh.Fill(HeadSVG);
        TailMesh.Fill(TailSVG);
        BodyMesh.Fill(BodySVG);
    }

    private void Update(float dt)
    {
        Interval -= dt;
        FollowTime -= dt;

        if (Interval <= 0f)
        {
            To = RandomField();
            To = Head + Vector2.ClampMagnitude(To - Head, 2f);
            HeadR = R * 0.15f;
            TailR = R;
            Interval = Random.Range(1.5f, 2.6f) * IntervalRate;
            FollowTime = Random.Range(0.15f, 0.25f);
        }

        Head = Vector2.Lerp(To, Head, Mathf.Exp(-5f * dt));

        if (FollowTime <= 0f)
        {
            Tail = Vector2.Lerp(Head, Tail, Mathf.Exp(-5f * dt));

            if (FollowTime <= -0.4f)
            {
                TailR = Mathf.Lerp(R, TailR, Mathf.Exp(-4f * dt));
            }
            else
            {
                TailR = Mathf.Lerp(R * 0.05f, TailR, Mathf.Exp(-6f * dt));
            }
            
            HeadR = Mathf.Lerp(R, HeadR, Mathf.Exp(-3f * dt));
        }
    }

    private Vector2 RandomField()
    {
        return new Vector2(Random.Range(-4f, 4f), Random.Range(-4f, 4f));
    }

    private void Circle(SVGData svg, Vector2 c, float r)
    {
        for (var i = 0; i < 4; ++i)
        {
            var angle0 = Mathf.PI * 0.5f * (i + 0);
            var angle1 = Mathf.PI * 0.5f * (i + 1);

            var x0 = c.x + Mathf.Cos(angle0) * r;
            var y0 = c.y - Mathf.Sin(angle0) * r;
            var x1 = c.x + Mathf.Cos(angle1) * r;
            var y1 = c.y - Mathf.Sin(angle1) * r;

            var a = r * (4f / 3f) * Mathf.Tan((angle1 - angle0) / 4f);
            var inAngle = angle0 + Mathf.PI * 0.5f;
            var inX = x0 + Mathf.Cos(inAngle) * a;
            var inY = y0 - Mathf.Sin(inAngle) * a;
            var outAngle = angle1 - Mathf.PI * 0.5f;
            var outX = x1 + Mathf.Cos(outAngle) * a;
            var outY = y1 - Mathf.Sin(outAngle) * a;

            if (i == 0)
            {
                svg.Move(x0, y0);
            }
            
            svg.Curve(inX, inY, outX, outY, x1, y1);
        }
    }
    
    // http://shspage.com/aijs/

    private void Metaball(SVGData svg, Vector2 c1, float r1, Vector2 c2, float r2, float v)
    {
        if (r1 == 0f || r2 == 0f)
        {
            return;
        }
  
        var pi2 = Mathf.PI / 2f;

        var d = (c2 - c1).magnitude;

        var u1 = 0f;
        var u2 = 0f;
        if (d <= Mathf.Abs(r1 - r2))
        {
            return;
        }
        else if (d < r1 + r2)
        {
            // case circles are overlapping
            u1 = Mathf.Acos((r1 * r1 + d * d - r2 * r2) / (2 * r1 * d));
            u2 = Mathf.Acos((r2 * r2 + d * d - r1 * r1) / (2 * r2 * d));
        }

        var t1 = Mathf.Atan2(c2.y - c1.y, c2.x - c1.x);
        var t2 = Mathf.Acos((r1 - r2) / d);
  
        var t1a = t1 + u1 + (t2 - u1) * v;
        var t1b = t1 - u1 - (t2 - u1) * v;
        var t2a = t1 + Mathf.PI - u2 - (Mathf.PI - u2 - t2) * v;
        var t2b = t1 - Mathf.PI + u2 + (Mathf.PI - u2 - t2) * v;
  
        var p1a = PointOnCircle(c1, t1a, r1);
        var p1b = PointOnCircle(c1, t1b, r1);
        var p2a = PointOnCircle(c2, t2a, r2);
        var p2b = PointOnCircle(c2, t2b, r2);

        // define handle length by the distance between both ends of the curve to draw
        var handle_len_rate = 2;
        var d2 = Mathf.Min(v * handle_len_rate, (p2a - p1a).magnitude / (r1 + r2));
        d2 *= Mathf.Min(1, d * 2 / (r1 + r2)); // case circles are overlapping
        r1 *= d2;
        r2 *= d2;
        
        svg.Move(p1a);
        svg.Curve(PointOnCircle(p1a, t1a - pi2, r1), PointOnCircle(p2a, t2a + pi2, r2), p2a);
        svg.Line(p2b);
        svg.Curve(PointOnCircle(p2b, t2b - pi2, r2), PointOnCircle(p1b, t1b + pi2, r1), p1b);
        svg.Line(p1a);
    }

    private Vector2 PointOnCircle(Vector2 c, float angle, float r)
    {
        return c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }
}
```

Use `SVGData.Move`, `SVGData.Line`, `SVGData.Curve`, ... and so on. Create realtime path as you like.

### Options

The following options are provided in SVGMesh component.

- `Delaunay` (default `false`)
  - whether to use Delaunay triangulation
  - Delaunay triangulation is slower, but looks better
- `Scale` (default `1`)
  - a positive number, the scale at which to [approximate the curves](https://github.com/mattdesl/adaptive-bezier-curve) from the SVG paths
  - higher number leads to smoother corners, but slower triangulation
- `Interior` if set, only return interior faces. See note. (Default `true`)
- `Exterior` if set, only return exterior faces. See note. (Default `false`)
- `Infinity` if set, then the triangulation is augmented with a [point at infinity](https://en.wikipedia.org/wiki/Point_at_infinity) represented by the index `-1`.  (Default `false`)

## License

MIT
