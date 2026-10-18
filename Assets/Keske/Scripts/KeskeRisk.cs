using UnityEngine;

namespace Keske
{
    public class KeskeRisk : MonoBehaviour
    {
        public Transform movingPart;
        public Vector3 initialPosition;
        public Vector3 initialEuler;
        public Vector3 preparedPosition;
        public Vector3 preparedEuler;
        public Vector3 aftermathPosition;
        public Vector3 aftermathEuler;
        public bool needsTool;
        public GameObject strap;
        [TextArea] public string resultTR;
        [TextArea] public string resultEN;
        [TextArea] public string learningTR;
        [TextArea] public string learningEN;

        public void Apply(KeskeGame g)
        {
            if (movingPart == null) return;
            bool done = g.progress.Fixed(GetComponent<KeskeInteractable>().id);
            movingPart.localPosition = done ? preparedPosition : (g.preview ? aftermathPosition : initialPosition);
            movingPart.localEulerAngles = done ? preparedEuler : (g.preview ? aftermathEuler : initialEuler);
            if (strap != null) strap.SetActive(done);
        }

        public void Interact(KeskeGame g)
        {
            var item = GetComponent<KeskeInteractable>();
            if (g.progress.Fixed(item.id))
            {
                g.ShowNote(item.Title(g), g.T(learningTR, learningEN));
                return;
            }
            if (g.preview)
            {
                if (!g.progress.observedRisks.Contains(item.id)) g.progress.observedRisks.Add(item.id);
                g.ShowNote(item.Title(g), g.T(resultTR, resultEN) + "\n\n" + g.T("Q ile hazırlığa dön ve bu sonucu değiştir.", "Return with Q and change this outcome."));
                g.Save();
                return;
            }
            if (needsTool && !g.progress.Has("tool"))
            {
                g.Notify(g.T("Sabitleme seti gerekiyor. Yatak odasındaki çalışma masasını kontrol et.", "You need the fastening kit. Check the bedroom study desk."));
                return;
            }
            g.progress.Fix(item.id);
            Apply(g);
            Physics.SyncTransforms();
            g.Prepared(item.Title(g) + " · " + g.T("Önlem alındı", "Prepared"));
            g.ShowNote(item.Title(g), g.T(learningTR, learningEN));
        }
    }
}
