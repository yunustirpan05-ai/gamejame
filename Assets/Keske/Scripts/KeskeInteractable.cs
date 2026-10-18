using UnityEngine;

namespace Keske
{
    public enum InteractionKind { Pickup, Door, Risk, Bag, Plan, Note, Finish }

    public class KeskeInteractable : MonoBehaviour
    {
        public InteractionKind kind;
        public string id;
        public string titleTR;
        public string titleEN;
        [TextArea(2, 8)] public string bodyTR;
        [TextArea(2, 8)] public string bodyEN;
        public bool recordMemory = true;
        public string requiredItem;
        public Transform doorLeaf;
        public float openAngle = 95;
        public bool openAwayFromPlayer;
        public Vector3 doorCenter;
        float activeOpenAngle;
        public bool opened;
        public bool startOpen;
        public GameObject marker;
        Quaternion closedRotation;
        bool doorInitialized;

        public string Title(KeskeGame g) { return g.T(titleTR, titleEN); }
        public string Body(KeskeGame g) { return g.T(bodyTR, bodyEN); }

        public void Initialize()
        {
            if (doorLeaf != null && !doorInitialized)
            {
                closedRotation = doorLeaf.localRotation;
                activeOpenAngle = openAngle;
                doorInitialized = true;
            }
        }

        void Update()
        {
            if (doorLeaf == null) return;
            Initialize();
            Quaternion target = closedRotation * Quaternion.Euler(0, opened ? activeOpenAngle : 0, 0);
            doorLeaf.localRotation = Quaternion.Slerp(doorLeaf.localRotation, target, 8 * Time.deltaTime);
        }

        public void ResetDoor()
        {
            Initialize();
            opened = startOpen;
            activeOpenAngle = openAngle;
            if (doorLeaf != null) doorLeaf.localRotation = closedRotation * Quaternion.Euler(0, opened ? activeOpenAngle : 0, 0);
        }

        public string Prompt(KeskeGame g)
        {
            if (kind == InteractionKind.Door) return g.T(opened ? "Kapat" : "Aç", opened ? "Close" : "Open") + " · " + Title(g);
            if (kind == InteractionKind.Note) return g.T("Oku", "Read") + " · " + Title(g);
            if (kind == InteractionKind.Risk)
            {
                if (g.progress.Fixed(id)) return g.T("İncele · " + (GetComponent<KeskeRisk>().needsTool ? "SABİTLENDİ" : "TAMAMLANDI"), "Inspect · COMPLETED") + " · " + Title(g);
                return g.T(g.preview ? "Sonucu incele" : "Önlem al", g.preview ? "Inspect outcome" : "Prepare") + " · " + Title(g);
            }
            if (g.preview && kind != InteractionKind.Finish) return g.T("İncele · Hazırlık için Q", "Inspect · Q to prepare") + " · " + Title(g);
            if (kind == InteractionKind.Pickup) return g.T("Al · Çanta malzemesi", "Collect · Bag supply") + " · " + Title(g);
            if (kind == InteractionKind.Bag) return g.T(g.progress.bagPacked ? "İncele · Çanta hazır" : "Malzemeleri çantaya koy", g.progress.bagPacked ? "Inspect · Bag packed" : "Pack supplies");
            if (kind == InteractionKind.Plan) return g.T(g.progress.familyPlan ? "İncele · Plan tamamlandı" : "Aile planını oluştur", g.progress.familyPlan ? "Inspect · Plan complete" : "Create family plan");
            return g.T("Etkileş", "Interact") + " · " + Title(g);
        }

        public void Use(KeskeGame g)
        {
            if (kind == InteractionKind.Door)
            {
                if (!string.IsNullOrEmpty(requiredItem) && !g.progress.Has(requiredItem))
                {
                    g.Notify(g.T("Kapı kilitli; gereken eşyayı bul.", "Locked; find the required item."));
                    return;
                }
                if (!opened)
                {
                    Initialize();
                    activeOpenAngle = openAngle;
                    if (openAwayFromPlayer && doorLeaf != null && g.player != null)
                    {
                        Vector3 centerOffset = doorLeaf.TransformVector(doorCenter);
                        Vector3 towardPlayer = g.player.transform.position - doorLeaf.position;
                        Vector3 openingDirection = Vector3.Cross(doorLeaf.parent != null ? doorLeaf.parent.up : Vector3.up, centerOffset);
                        activeOpenAngle = Mathf.Abs(openAngle) * (Vector3.Dot(openingDirection, towardPlayer) > 0 ? -1 : 1);
                    }
                }
                opened = !opened;
                g.sound.Play("door");
                return;
            }
            if (kind == InteractionKind.Note)
            {
                if (recordMemory && !g.progress.memories.Contains(id)) g.progress.memories.Add(id);
                g.ShowNote(Title(g), Body(g));
                g.Save();
                return;
            }
            if (kind == InteractionKind.Risk)
            {
                GetComponent<KeskeRisk>().Interact(g);
                return;
            }
            if (kind == InteractionKind.Finish) { g.TryFinish(); return; }
            if (g.preview)
            {
                g.Notify(g.T("Burası bir sonuç önizlemesi. Hazırlık yapmak için Q ile geri dön.", "This is an outcome preview. Press Q to return to preparation."));
                return;
            }
            if (kind == InteractionKind.Pickup)
            {
                g.progress.Collect(id);
                gameObject.SetActive(false);
                g.sound.Play("pickup");
                g.Notify(g.T("Alındı: ", "Collected: ") + Title(g));
                g.Save();
            }
            else if (kind == InteractionKind.Bag)
            {
                if (g.progress.bagPacked) { g.ShowNote(Title(g), g.T("Dokuz örnek malzeme çantaya yerleştirildi. Çanta girişte erişilebilir durumda.", "All nine example supplies are packed. The bag is accessible near the entrance.")); return; }
                if (!g.progress.CanPack)
                {
                    g.Notify(g.T("Çanta için malzeme: ", "Bag supplies: ") + g.progress.SupplyCount + "/9. " + g.T("TAB ile listeyi gör; H ile ipucu al.", "TAB for the list; H for a hint."));
                    return;
                }
                g.progress.bagPacked = true;
                g.progress.Changed();
                g.Prepared(g.T("Çanta hazır ve girişte erişilebilir durumda.", "The bag is packed and accessible by the entrance."));
            }
            else if (kind == InteractionKind.Plan)
            {
                if (g.progress.familyPlan) { g.ShowNote(Title(g), g.T("Aile planı tamamlandı: önceden belirlenmiş buluşma yeri ve bölge dışı iletişim kişisi.", "Family plan complete: an agreed meeting place and an out-of-area contact.")); return; }
                g.ShowPlan(this);
            }
        }
    }
}
