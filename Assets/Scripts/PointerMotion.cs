using UnityEngine;
namespace TiltBrush
{
    public class PointerMotion
    {

        public static void Spinny(PointerManager.PointerData[] pointers)
        {
            const float TWOPI = Mathf.PI * 2;
            float freq = 1f;
            float amp = 2f / pointers[0].m_Script.BrushSize01;
            float theta = Time.fixedTime * freq * TWOPI;

            for (var i = 0; i < pointers.Length; i++)
            {
                var pointer = pointers[i];
                var xf = pointer.m_Script.transform;

                float phase = (TWOPI / pointers.Length) * i;
                var offset = new Vector3(
                    Mathf.Sin(theta + phase) / amp,
                    Mathf.Cos(theta + phase) / amp,
                    0
                );
                offset = xf.rotation * offset;
                xf.position += offset;
            }

            // var xf0 = pointers[0].m_Script.transform;
            //
            // var offset0 = new Vector3(
            //     Mathf.Sin(theta + TWOPI / 2f) / amp,
            //     Mathf.Cos(theta + TWOPI / 2f) / amp,
            //     0
            // );
            // offset0 = xf0.rotation * offset0;
            // xf0.position += offset0;


            // var xf1 = pointers[1].m_Script.transform;
            // xf1.rotation = xf0.rotation;
            // var offset1 = new Vector3(
            //     Mathf.Sin(theta) / amp,
            //     Mathf.Cos(theta) / amp,
            //     0
            // );
            // offset1 = xf0.rotation * offset1;
            // xf1.position = xf0.position + offset1;
        }

        public static void SpinnyColors(PointerManager.PointerData[] pointers)
        {
            var newColor = pointers[0].m_Script.GetCurrentColor();
            newColor = new Color(newColor.b, newColor.g, newColor.r);
            pointers[1].m_Script.SetColor(newColor);
        }
    }
}
