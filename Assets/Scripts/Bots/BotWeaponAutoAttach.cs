using UnityEngine;
using UnityEngine.Animations; // ParentConstraint (se existir)

public class BotWeaponAutoFit : MonoBehaviour
{
    public enum Source { UseExistingChildWeapon, InstantiatePrefab, FindSceneByNameAndClone }
    public enum Hand   { HumanoidRight, HumanoidLeft, ByBoneName }

    [Header("Como obter a arma")]
    public Source source = Source.UseExistingChildWeapon;
    public GameObject weaponPrefab;       // se Source=InstantiatePrefab
    public string sceneWeaponName = "";   // se Source=FindSceneByNameAndClone

    [Header("Onde prender")]
    public Hand hand = Hand.HumanoidRight;
    public string handBoneName = "RightHand"; // se ByBoneName (ex.: "mixamorig:RightHand")

    [Header("Nomes dos marcadores na arma")]
    public string gripName   = "Grip";       // empty na arma onde a mão segura
    public string muzzleName = "FirePoint";  // já tens

    [Header("Opções de fixação")]
    public bool useParentConstraintIfAvailable = true;
    public bool alsoReapplyInLateUpdate = true;
    public bool alignMuzzleForwardWithHand = true; // tenta alinhar FirePoint com direção da mão

    Transform handBone;        // o osso da mão
    Transform socket;          // criado debaixo da mão
    GameObject weaponGO;       // instância/objeto da arma
    Transform grip;            // marcador "Grip" na arma
    Transform muzzle;          // FirePoint/Muzzle

    void Start()
    {
        // 1) achar a mão
        var anim = GetComponentInChildren<Animator>();
        if (!anim) { Debug.LogWarning("[AutoFit] Sem Animator no bot."); return; }

        switch (hand)
        {
            case Hand.HumanoidRight: if (anim.isHuman) handBone = anim.GetBoneTransform(HumanBodyBones.RightHand); break;
            case Hand.HumanoidLeft : if (anim.isHuman) handBone = anim.GetBoneTransform(HumanBodyBones.LeftHand);  break;
            case Hand.ByBoneName   : handBone = FindRecursive(anim.transform, handBoneName); break;
        }

        if (!handBone) // fallbacks comuns
            handBone = FindRecursive(anim.transform, "RightHand") ??
                       FindRecursive(anim.transform, "mixamorig:RightHand");

        if (!handBone) { Debug.LogWarning("[AutoFit] Osso da mão não encontrado."); return; }

        // 2) criar um socket limpo na mão
        socket = new GameObject("WeaponSocket_Auto").transform;
        socket.SetParent(handBone, false);
        socket.localPosition = Vector3.zero;
        socket.localRotation = Quaternion.identity;
        socket.localScale    = Vector3.one;

        // 3) obter a arma
        weaponGO = AcquireWeapon();
        if (!weaponGO) { Debug.LogWarning("[AutoFit] Não consegui obter a arma."); return; }

        // garantir que animação da arma não interfere
        var wAnim = weaponGO.GetComponent<Animator>();
        if (wAnim) wAnim.enabled = false;

        // 4) apanhar marcadores
        grip   = FindRecursive(weaponGO.transform, gripName);
        muzzle = FindRecursive(weaponGO.transform, muzzleName);
        if (!grip)
        {
            Debug.LogWarning($"[AutoFit] Marcador '{gripName}' não encontrado na arma. Abort.");
            return;
        }
        if (!muzzle) Debug.LogWarning($"[AutoFit] Marcador '{muzzleName}' não encontrado. Vou alinhar só pelo Grip.");

        // 5) parentear e auto-alinhar
        // parent primeiro mantendo world pose
        weaponGO.transform.SetParent(socket, true);

        // Queremos: grip.localPosition == (0,0,0) e grip.localRotation == identidade sob o socket.
        // Para isso, ajustamos a arma (não o Grip):
        // regra: aplicar rotação primeiro, depois posição.
        // fazer o grip virar identidade:
        weaponGO.transform.localRotation = weaponGO.transform.localRotation * Quaternion.Inverse(grip.localRotation);
        // depois transladar de modo a "anular" a posição local do grip
        weaponGO.transform.localPosition -= grip.localPosition;

        // 6) opcional: alinhar o FirePoint para frente da mão/socket
        if (alignMuzzleForwardWithHand && muzzle)
        {
            // alinhar direção: aponte o muzzle.forward para socket.forward
            Vector3 from = muzzle.forward;
            Vector3 to   = socket.forward;
            Quaternion rot = Quaternion.FromToRotation(from, to);
            weaponGO.transform.rotation = rot * weaponGO.transform.rotation;

            // após girar a arma, o Grip pode ter saído um pouco — zera de novo
            weaponGO.transform.localRotation = weaponGO.transform.localRotation * Quaternion.Inverse(grip.localRotation);
            weaponGO.transform.localPosition -= grip.localPosition;
        }

        // 7) Constraint opcional para estabilidade extra
        if (useParentConstraintIfAvailable)
        {
            var pc = weaponGO.AddComponent<ParentConstraint>();
            var src = new ConstraintSource { sourceTransform = socket, weight = 1f };
            pc.AddSource(src);
            pc.constraintActive = true;
            pc.locked = false;
            pc.weight = 1f;
            // manter offsets atuais
            pc.translationAtRest = Vector3.zero;
            pc.rotationAtRest    = Vector3.zero;
        }

        // garantir que a arma do bot não usa Camera (Weapon)
        var w = weaponGO.GetComponent<Weapon>();
        if (w)
        {
            // se tiver propriedade pública para câmera, força a null (ignora se não existir)
            var camField = w.GetType().GetField("cam", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (camField != null) camField.SetValue(w, null);
        }
    }

    void LateUpdate()
    {
        // resiliente a variações de pose: re-aplica offsets relativos
        if (alsoReapplyInLateUpdate && socket && weaponGO && grip)
        {
            weaponGO.transform.localRotation = weaponGO.transform.localRotation * Quaternion.Inverse(grip.localRotation);
            weaponGO.transform.localPosition -= grip.localPosition;
        }
    }

    GameObject AcquireWeapon()
    {
        if (source == Source.UseExistingChildWeapon)
        {
            var w = GetComponentInChildren<Weapon>(true);
            if (w) return w.gameObject;
        }
        else if (source == Source.InstantiatePrefab)
        {
            if (weaponPrefab) return Instantiate(weaponPrefab);
        }
        else if (source == Source.FindSceneByNameAndClone)
        {
            if (!string.IsNullOrEmpty(sceneWeaponName))
            {
                var inScene = GameObject.Find(sceneWeaponName);
                if (inScene) return Instantiate(inScene);
            }
        }
        return null;
    }

    Transform FindRecursive(Transform root, string name)
    {
        if (!root) return null;
        foreach (Transform t in root)
        {
            if (t.name == name) return t;
            var r = FindRecursive(t, name);
            if (r) return r;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        if (socket)
        {
            Gizmos.color = Color.green; Gizmos.DrawRay(socket.position, socket.up * 0.05f);
            Gizmos.color = Color.red;   Gizmos.DrawRay(socket.position, socket.right * 0.05f);
            Gizmos.color = Color.blue;  Gizmos.DrawRay(socket.position, socket.forward * 0.1f);
        }
    }
}
