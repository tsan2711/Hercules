using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Quản lý di chuyển và attack của các quân cờ với sequence actions
/// Thay thế cho việc set position ngay lập tức
/// </summary>
public class ChessPieceController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private MoveType moveType = MoveType.Walk;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float jumpHeight = 1f;
    
    [Header("Attack Settings")]
    [SerializeField] private AttackType attackType = AttackType.MeleeAfterMove;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private AnimationCurve attackCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("VFX Settings")]
    [SerializeField] private Transform vfxSpawnPoint; // VFX spawn point (VFX prefabs managed by VFXManager)
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip teleportSound;
    
    // Private fields
    private ChessPieceInfo pieceInfo;
    private ChessPieceSkinController skinController;
    private bool isMoving = false;
    private bool isAttacking = false;
    private Coroutine currentActionCoroutine;
    private Queue<System.Action> actionQueue = new Queue<System.Action>();
    
    // VFX tracking
    private VFXInstance currentMoveVFX;
    private VFXInstance currentAttackVFX;
    private VFXInstance currentTeleportVFX;
    
    // Events
    public System.Action<ChessPieceController> OnMoveStarted;
    public System.Action<ChessPieceController> OnMoveCompleted;
    public System.Action<ChessPieceController, ChessPieceInfo> OnAttackStarted;
    public System.Action<ChessPieceController, ChessPieceInfo> OnAttackCompleted;
    public System.Action<ChessPieceController> OnActionSequenceCompleted;
    
    // Properties
    public bool IsMoving => isMoving;
    public bool IsAttacking => isAttacking;
    public bool IsBusy => isMoving || isAttacking || actionQueue.Count > 0;
    public MoveType CurrentMoveType => moveType;
    public AttackType CurrentAttackType => attackType;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        pieceInfo = GetComponent<ChessPieceInfo>();
        skinController = GetComponent<ChessPieceSkinController>();
        
        if (pieceInfo == null)
        {
            Debug.LogError($"ChessPieceController requires ChessPieceInfo component on {gameObject.name}");
            return;
        }
        
        if (skinController == null)
        {
            skinController = gameObject.AddComponent<ChessPieceSkinController>();
        }
        
        if (vfxSpawnPoint == null)
        {
            vfxSpawnPoint = transform;
        }
        
        // Set move and attack types based on piece type
        SetupPieceTypeDefaults();
    }
    
    /// <summary>
    /// Thiết lập move type và attack type mặc định dựa trên loại quân cờ
    /// </summary>
    private void SetupPieceTypeDefaults()
    {
        if (pieceInfo == null) return;
        
        switch (pieceInfo.type)
        {
            case ChessRaycastDebug.ChessType.Pawn:
                moveType = MoveType.Walk;
                attackType = AttackType.CastSpellBeforeMove;
                moveSpeed = 1.5f;
                moveDuration = 1.2f;
                break;
                
            case ChessRaycastDebug.ChessType.Knight:
                moveType = MoveType.Jump;
                attackType = AttackType.CastSpellBeforeMove;
                moveSpeed = 2f;
                moveDuration = 1f;
                jumpHeight = 2f;
                break;
                
            case ChessRaycastDebug.ChessType.Bishop:
                moveType = MoveType.Teleport;
                attackType = AttackType.CastSpellBeforeMove;
                moveSpeed = 3f;
                moveDuration = 0.5f;
                break;
                
            case ChessRaycastDebug.ChessType.Rook:
                moveType = MoveType.Slide;
                attackType = AttackType.CastSpellBeforeMove;
                moveSpeed = 2.5f;
                moveDuration = 0.8f;
                break;
                
            case ChessRaycastDebug.ChessType.Queen:
                moveType = MoveType.Float;
                attackType = AttackType.CastSpellBeforeMove;
                moveSpeed = 2f;
                moveDuration = 1f;
                break;
                
            case ChessRaycastDebug.ChessType.King:
                moveType = MoveType.Walk;
                attackType = AttackType.CastSpellBeforeMove;
                moveSpeed = 1f;
                moveDuration = 1.5f;
                break;
        }
    }
    
    /// <summary>
    /// Entry point chính cho việc di chuyển quân cờ
    /// Thay thế ChessPieceMover.MovePiece()
    /// </summary>
    /// <param name="targetWorldPos">Vị trí đích trong world space</param>
    public void MovePiece(Vector3 targetWorldPos)
    {
        if (pieceInfo == null || ChessBoardManager.Instance == null) return;
        
        // Kiểm tra xem piece có đang busy không
        if (IsBusy)
        {
            Debug.LogWarning($"ChessPieceController on {gameObject.name} is busy, cannot move!");
            return;
        }
        
        Vector2Int start = pieceInfo.boardPosition;
        Vector2Int target = ChessBoardManager.Instance.WorldToBoard(targetWorldPos);
        
        // Kiểm tra nhập thành
        if (pieceInfo.type == ChessRaycastDebug.ChessType.King && Mathf.Abs(target.x - start.x) == 2)
        {
            HandleCastling(target, start, targetWorldPos);
            return;
        }
        
        // Di chuyển bình thường
        ChessPieceInfo targetPiece = ChessBoardManager.Instance.board[target.x, target.y];
        bool isAttackMove = targetPiece != null && targetPiece != pieceInfo;
        
        // Sử dụng ExecuteMove với animation
        ExecuteMove(targetWorldPos, isAttackMove ? targetPiece : null, () => {
            HandlePostMoveLogic(target);
        });
    }
    
    /// <summary>
    /// Xử lý nhập thành
    /// </summary>
    private void HandleCastling(Vector2Int target, Vector2Int start, Vector3 targetWorldPos)
    {
        int y = start.y;
        
        // Nhập thành nhỏ (king-side)
        if (target.x > start.x)
        {
            ChessPieceInfo rook = ChessBoardManager.Instance.board[7, y];
            if (rook != null && rook.type == ChessRaycastDebug.ChessType.Rook && !rook.hasMoved)
            {
                ChessPieceController rookController = rook.GetComponent<ChessPieceController>();
                Vector3 rookTargetPos = ChessBoardManager.Instance.BoardToWorld(5, y);
                
                if (rookController != null)
                {
                    // Di chuyển vua trước
                    ExecuteMove(targetWorldPos, null, () => {
                        // Sau đó di chuyển xe
                        rookController.ExecuteMove(rookTargetPos, null, () => {
                            CompleteCastling(rook, target, new Vector2Int(5, y), start);
                        });
                    });
                }
                else
                {
                    // Fallback: Animate both pieces with DOTween
                    Sequence castlingSequence = DOTween.Sequence();
                    castlingSequence.Append(transform.DOMove(targetWorldPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.Join(rook.transform.DOMove(rookTargetPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.OnComplete(() => {
                        CompleteCastling(rook, target, new Vector2Int(5, y), start);
                    });
                }
                return;
            }
        }
        // Nhập thành lớn (queen-side)
        else
        {
            ChessPieceInfo rook = ChessBoardManager.Instance.board[0, y];
            if (rook != null && rook.type == ChessRaycastDebug.ChessType.Rook && !rook.hasMoved)
            {
                ChessPieceController rookController = rook.GetComponent<ChessPieceController>();
                Vector3 rookTargetPos = ChessBoardManager.Instance.BoardToWorld(3, y);
                
                if (rookController != null)
                {
                    // Di chuyển vua trước
                    ExecuteMove(targetWorldPos, null, () => {
                        // Sau đó di chuyển xe
                        rookController.ExecuteMove(rookTargetPos, null, () => {
                            CompleteCastling(rook, target, new Vector2Int(3, y), start);
                        });
                    });
                }
                else
                {
                    // Fallback: Animate both pieces with DOTween
                    Sequence castlingSequence = DOTween.Sequence();
                    castlingSequence.Append(transform.DOMove(targetWorldPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.Join(rook.transform.DOMove(rookTargetPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.OnComplete(() => {
                        CompleteCastling(rook, target, new Vector2Int(3, y), start);
                    });
                }
                return;
            }
        }
    }
    
    /// <summary>
    /// Hoàn thành logic nhập thành
    /// </summary>
    private void CompleteCastling(ChessPieceInfo rook, Vector2Int kingTarget, Vector2Int rookTarget, Vector2Int kingStart)
    {
        // Cập nhật board positions
        pieceInfo.boardPosition = kingTarget;
        ChessBoardManager.Instance.board[kingStart.x, kingStart.y] = null;
        ChessBoardManager.Instance.board[kingTarget.x, kingTarget.y] = pieceInfo;
        pieceInfo.hasMoved = true;

        rook.boardPosition = rookTarget;
        ChessBoardManager.Instance.board[7, kingStart.y] = null; // or 0 for queenside
        ChessBoardManager.Instance.board[rookTarget.x, rookTarget.y] = rook;
        rook.hasMoved = true;
        
        // Chuyển lượt chơi sau nhập thành
        if (ChessBoardManager.Instance != null)
            ChessBoardManager.Instance.EndTurn();
    }
    
    /// <summary>
    /// Xử lý logic sau di chuyển (pawn promotion, end turn)
    /// </summary>
    private void HandlePostMoveLogic(Vector2Int target)
    {
        // Kiểm tra phong cấp tốt
        if (pieceInfo.type == ChessRaycastDebug.ChessType.Pawn)
        {
            CheckPawnPromotion(target);
        }
        
        // Chuyển lượt chơi
        if (ChessBoardManager.Instance != null)
            ChessBoardManager.Instance.EndTurn();
    }
    
    /// <summary>
    /// Kiểm tra và thực hiện phong cấp tốt
    /// </summary>
    private void CheckPawnPromotion(Vector2Int targetPos)
    {
        // Kiểm tra tốt có đến cuối bàn cờ không
        bool reachedEnd = (pieceInfo.isWhite && targetPos.y == 7) || (!pieceInfo.isWhite && targetPos.y == 0);
        
        if (reachedEnd)
        {
            Debug.Log($"Pawn promotion! {(pieceInfo.isWhite ? "White" : "Black")} pawn reached the end!");
            
            // Phong cấp thành Hậu (Queen)
            PromotePawn(ChessRaycastDebug.ChessType.Queen);
        }
    }
    
    /// <summary>
    /// Thực hiện phong cấp tốt
    /// </summary>
    private void PromotePawn(ChessRaycastDebug.ChessType newType)
    {
        Vector2Int pos = pieceInfo.boardPosition;
        bool isWhite = pieceInfo.isWhite;
        
        // Xóa tốt cũ
        Destroy(gameObject);
        ChessBoardManager.Instance.board[pos.x, pos.y] = null;
        
        // Tạo quân mới
        GameObject newPiecePrefab = GetPromotionPrefab(isWhite, newType);
        if (newPiecePrefab != null)
        {
            Vector3 worldPos = ChessBoardManager.Instance.BoardToWorld(pos.x, pos.y);
            
            // Logic xoay cho quân phong cấp: chỉ quân trắng xoay 180 độ, quân đen giữ nguyên
            Quaternion rot = isWhite ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            
            GameObject newPieceObj = Instantiate(newPiecePrefab, worldPos, rot);
            
            // Đảm bảo quân phong cấp có đúng hướng
            newPieceObj.transform.rotation = rot;
            
            Debug.Log($"Promoted piece rotation set to: {rot.eulerAngles} for {(isWhite ? "White" : "Black")} {newType}");
            
            ChessPieceInfo newPieceInfo = newPieceObj.GetComponent<ChessPieceInfo>();
            newPieceInfo.isWhite = isWhite;
            newPieceInfo.type = newType;
            newPieceInfo.boardPosition = pos;
            newPieceInfo.hasMoved = true; // Quân phong cấp đã được coi là đã di chuyển
            
            ChessBoardManager.Instance.board[pos.x, pos.y] = newPieceInfo;
            
            Debug.Log($"Pawn promoted to {newType}!");
        }
    }
    
    /// <summary>
    /// Lấy prefab cho quân phong cấp
    /// </summary>
    private GameObject GetPromotionPrefab(bool isWhite, ChessRaycastDebug.ChessType type)
    {
        ChessBoardManager boardManager = ChessBoardManager.Instance;
        
        if (isWhite)
        {
            switch (type)
            {
                case ChessRaycastDebug.ChessType.Queen: return boardManager.whiteQueenPrefab;
                case ChessRaycastDebug.ChessType.Rook: return boardManager.whiteRookPrefab;
                case ChessRaycastDebug.ChessType.Bishop: return boardManager.whiteBishopPrefab;
                case ChessRaycastDebug.ChessType.Knight: return boardManager.whiteKnightPrefab;
            }
        }
        else
        {
            switch (type)
            {
                case ChessRaycastDebug.ChessType.Queen: return boardManager.blackQueenPrefab;
                case ChessRaycastDebug.ChessType.Rook: return boardManager.blackRookPrefab;
                case ChessRaycastDebug.ChessType.Bishop: return boardManager.blackBishopPrefab;
                case ChessRaycastDebug.ChessType.Knight: return boardManager.blackKnightPrefab;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Thực hiện di chuyển với attack sequence nếu cần
    /// </summary>
    /// <param name="targetWorldPos">Vị trí đích</param>
    /// <param name="targetPiece">Quân cờ bị tấn công (null nếu chỉ di chuyển)</param>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    public void ExecuteMove(Vector3 targetWorldPos, ChessPieceInfo targetPiece = null, System.Action onComplete = null)
    {
        if (IsBusy)
        {
            Debug.LogWarning($"ChessPieceController on {gameObject.name} is busy, queueing action");
            actionQueue.Enqueue(() => ExecuteMove(targetWorldPos, targetPiece, onComplete));
            return;
        }
        
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
        }
        
        currentActionCoroutine = StartCoroutine(ExecuteMoveSequence(targetWorldPos, targetPiece, onComplete));
    }
    
    /// <summary>
    /// Coroutine thực hiện sequence di chuyển và attack
    /// </summary>
    private IEnumerator ExecuteMoveSequence(Vector3 targetWorldPos, ChessPieceInfo targetPiece, System.Action onComplete)
    {
        Debug.Log($"ExecuteMoveSequence - AttackType: {attackType}, TargetPiece: {(targetPiece != null ? targetPiece.name : "null")}");
        Vector3 startPos = transform.position;
        bool isAttackMove = targetPiece != null;
        
        Debug.Log($"isAttackMove: {isAttackMove}");
        
        // Bắt đầu sequence
        if (isAttackMove)
        {
            OnAttackStarted?.Invoke(this, targetPiece);
        }
        else
        {
            OnMoveStarted?.Invoke(this);
        }
        
        // Thực hiện pre-move attack nếu cần
        if (isAttackMove && (attackType == AttackType.RangedBeforeMove || attackType == AttackType.RangedThenMove))
        {
            Debug.Log("Executing RangedBeforeMove/RangedThenMove");
            yield return StartCoroutine(ExecuteAttackSequence(targetPiece, startPos));
        }
        
        // Thực hiện cast spell trước khi di chuyển nếu cần
        if (isAttackMove && attackType == AttackType.CastSpellBeforeMove)
        {
            Debug.Log("Executing CastSpellBeforeMove");
            yield return StartCoroutine(ExecuteCastSpellSequence(targetPiece, startPos));
        }
        
        // Thực hiện di chuyển
        yield return StartCoroutine(ExecuteMoveAnimation(startPos, targetWorldPos));
        
        // Thực hiện post-move attack nếu cần
        if (isAttackMove && (attackType == AttackType.MeleeAfterMove || attackType == AttackType.MoveThenRanged))
        {
            yield return StartCoroutine(ExecuteAttackSequence(targetPiece, targetWorldPos));
        }
        
        // Cập nhật board position
        UpdateBoardPosition(targetWorldPos);
        
        // Xử lý target piece nếu bị ăn
        if (isAttackMove && targetPiece != null)
        {
            HandleTargetPieceCapture(targetPiece);
            OnAttackCompleted?.Invoke(this, targetPiece);
        }
        else
        {
            OnMoveCompleted?.Invoke(this);
        }
        
        // Complete callback
        onComplete?.Invoke();
        OnActionSequenceCompleted?.Invoke(this);
        
        // Process next queued action
        ProcessNextQueuedAction();
    }
    
    /// <summary>
    /// Thực hiện animation di chuyển dựa trên move type
    /// </summary>
    private IEnumerator ExecuteMoveAnimation(Vector3 startPos, Vector3 targetPos)
    {
        isMoving = true;
        
        // Play move VFX
        currentMoveVFX = SpawnVFX(VFXType.Move, vfxSpawnPoint.position);
        
        // Play move sound
        PlaySound(moveSound);
        
        // Set skin to moving state
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Moving);
        }
        
        switch (moveType)
        {
            case MoveType.Walk:
                yield return StartCoroutine(WalkMovement(startPos, targetPos));
                break;
                
            case MoveType.Jump:
                yield return StartCoroutine(JumpMovement(startPos, targetPos));
                break;
                
            case MoveType.Slide:
                yield return StartCoroutine(SlideMovement(startPos, targetPos));
                break;
                
            case MoveType.Teleport:
                yield return StartCoroutine(TeleportMovement(startPos, targetPos));
                break;
                
            case MoveType.Float:
                yield return StartCoroutine(FloatMovement(startPos, targetPos));
                break;
        }
        
        // Reset skin state
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Normal);
        }
        
        isMoving = false;
    }
    
    /// <summary>
    /// Di chuyển dạng đi bộ (Pawn, King)
    /// </summary>
    private IEnumerator WalkMovement(Vector3 startPos, Vector3 targetPos)
    {
        float elapsed = 0f;
        
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / moveDuration;
            float curveValue = moveCurve.Evaluate(progress);
            
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, curveValue);
            transform.position = currentPos;
            
            yield return null;
        }
        
        transform.position = targetPos;
    }
    
    /// <summary>
    /// Di chuyển dạng nhảy (Knight)
    /// </summary>
    private IEnumerator JumpMovement(Vector3 startPos, Vector3 targetPos)
    {
        float elapsed = 0f;
        
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / moveDuration;
            float curveValue = moveCurve.Evaluate(progress);
            
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, curveValue);
            
            // Thêm arc jump
            float jumpProgress = Mathf.Sin(progress * Mathf.PI);
            currentPos.y += jumpHeight * jumpProgress;
            
            transform.position = currentPos;
            
            yield return null;
        }
        
        transform.position = targetPos;
    }
    
    /// <summary>
    /// Di chuyển dạng trượt (Rook)
    /// </summary>
    private IEnumerator SlideMovement(Vector3 startPos, Vector3 targetPos)
    {
        // Tương tự walk nhưng với curve khác
        yield return StartCoroutine(WalkMovement(startPos, targetPos));
    }
    
    /// <summary>
    /// Di chuyển dạng teleport (Bishop)
    /// </summary>
    private IEnumerator TeleportMovement(Vector3 startPos, Vector3 targetPos)
    {
        // Fade out
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Dissolving);
        }
        
        // Play teleport VFX và sound
        currentTeleportVFX = SpawnVFX(VFXType.Teleport, startPos);
        PlaySound(teleportSound);
        
        yield return new WaitForSeconds(moveDuration * 0.3f);
        
        // Teleport instantly
        transform.position = targetPos;
        
        // Spawn teleport VFX at destination
        SpawnVFX(VFXType.Teleport, targetPos);
        
        yield return new WaitForSeconds(moveDuration * 0.2f);
        
        // Fade in
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Normal);
        }
        
        yield return new WaitForSeconds(moveDuration * 0.5f);
    }
    
    /// <summary>
    /// Di chuyển dạng bay (Queen)
    /// </summary>
    private IEnumerator FloatMovement(Vector3 startPos, Vector3 targetPos)
    {
        float elapsed = 0f;
        float floatHeight = 0.5f;
        
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / moveDuration;
            float curveValue = moveCurve.Evaluate(progress);
            
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, curveValue);
            currentPos.y += floatHeight; // Float above ground
            
            transform.position = currentPos;
            
            yield return null;
        }
        
        // Land at target position
        transform.position = targetPos;
    }
    
    /// <summary>
    /// Thực hiện attack sequence
    /// </summary>
    private IEnumerator ExecuteAttackSequence(ChessPieceInfo targetPiece, Vector3 attackFromPos)
    {
        if (targetPiece == null) yield break;
        
        isAttacking = true;
        
        // Set skin to attacking state
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Attacking);
        }
        
        // Play attack VFX
        currentAttackVFX = SpawnVFX(VFXType.Attack, vfxSpawnPoint.position);
        
        // Play attack sound
        PlaySound(attackSound);
        
        // Determine attack direction
        Vector3 attackDirection = (targetPiece.transform.position - attackFromPos).normalized;
        
        Debug.Log($"Executing attack sequence with type: {attackType}");
        switch (attackType)
        {
            case AttackType.MeleeAfterMove:
                yield return StartCoroutine(DirectAttack(targetPiece, attackDirection));
                break;
                
            case AttackType.RangedBeforeMove:
            case AttackType.RangedThenMove:
            case AttackType.MoveThenRanged:
                yield return StartCoroutine(RangedAttack(targetPiece, attackDirection));
                break;
                
            case AttackType.CastSpellBeforeMove:
                // CastSpellBeforeMove được handle riêng trong ExecuteMoveSequence
                Debug.LogWarning("CastSpellBeforeMove should be handled in ExecuteMoveSequence, not ExecuteAttackSequence");
                break;
        }
        
        // Reset skin state
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Normal);
        }
        
        isAttacking = false;
    }
    
    /// <summary>
    /// Thực hiện cast spell sequence - cast spell trước, đợi dissolve xong rồi mới di chuyển
    /// </summary>
    private IEnumerator ExecuteCastSpellSequence(ChessPieceInfo targetPiece, Vector3 castFromPos)
    {
        isAttacking = true;
        
        // Trigger attack events
        OnAttackStarted?.Invoke(this, targetPiece);
        
        // Set attacking skin state
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Attacking);
        }
        
        // Spawn spell VFX tại vị trí cast
        currentAttackVFX = SpawnVFX(VFXType.Attack, castFromPos);
        
        // Play cast sound
        PlaySound(attackSound);
        
        // Cast spell animation (piece có thể có animation cast)
        yield return StartCoroutine(CastSpellAnimation(targetPiece, castFromPos));
        
        // Spawn projectile và đợi nó đến target
        yield return StartCoroutine(CastProjectileToTarget(targetPiece, castFromPos));
        
        // Đợi target piece dissolve hoàn toàn
        yield return StartCoroutine(WaitForTargetDissolve(targetPiece));
        
        // Reset skin state
        if (skinController != null)
        {
            skinController.SetSkinState(SkinState.Normal);
        }
        
        isAttacking = false;
    }
    
    /// <summary>
    /// Tấn công trực tiếp
    /// </summary>
    private IEnumerator DirectAttack(ChessPieceInfo targetPiece, Vector3 direction)
    {
        Vector3 originalPos = transform.position;
        Vector3 attackPos = originalPos + direction * 0.3f;
        
        // Lunge forward
        float elapsed = 0f;
        float halfDuration = attackDuration * 0.5f;
        
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / halfDuration;
            transform.position = Vector3.Lerp(originalPos, attackPos, attackCurve.Evaluate(progress));
            yield return null;
        }
        
        // Apply damage to target (trigger dissolve effect)
        if (targetPiece.GetComponent<ChessPieceSkinController>() != null)
        {
            targetPiece.GetComponent<ChessPieceSkinController>().SetSkinStateImmediate(SkinState.Dissolving);
        }
        
        // Return to original position
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / halfDuration;
            transform.position = Vector3.Lerp(attackPos, originalPos, attackCurve.Evaluate(progress));
            yield return null;
        }
        
        transform.position = originalPos;
    }
    
    /// <summary>
    /// Tấn công tầm xa
    /// </summary>
    private IEnumerator RangedAttack(ChessPieceInfo targetPiece, Vector3 direction)
    {
        // Spawn projectile VFX toward target
        Vector3 spawnPos = vfxSpawnPoint.position;
        Vector3 targetPos = targetPiece.transform.position;
        
        VFXInstance projectileVFX = SpawnVFX(VFXType.Attack, spawnPos, Quaternion.LookRotation(direction));
        
        // Animate projectile to target
        if (projectileVFX != null && projectileVFX.gameObject != null)
        {
            float projectileSpeed = attackRange / attackDuration;
            projectileVFX.gameObject.transform.DOMove(targetPos, attackDuration).OnComplete(() => {
                // Apply damage to target
                if (targetPiece.GetComponent<ChessPieceSkinController>() != null)
                {
                    targetPiece.GetComponent<ChessPieceSkinController>().SetSkinStateImmediate(SkinState.Dissolving);
                }
                
                // Despawn projectile VFX
                if (VFXManager.Instance != null)
                {
                    VFXManager.Instance.DespawnVFX(projectileVFX, true);
                }
            });
        }
        
        yield return new WaitForSeconds(attackDuration);
    }
    
    /// <summary>
    /// Cập nhật vị trí trên board
    /// </summary>
    private void UpdateBoardPosition(Vector3 targetWorldPos)
    {
        if (pieceInfo == null || ChessBoardManager.Instance == null) return;
        
        Vector2Int targetBoardPos = ChessBoardManager.Instance.WorldToBoard(targetWorldPos);
        ChessBoardManager.Instance.UpdateBoardPosition(pieceInfo, targetBoardPos);
        pieceInfo.hasMoved = true;
    }
    
    /// <summary>
    /// Xử lý việc ăn quân
    /// </summary>
    private void HandleTargetPieceCapture(ChessPieceInfo targetPiece)
    {
        if (targetPiece == null) return;
        
        // Trigger dissolve effect if has skin controller
        ChessPieceSkinController targetSkin = targetPiece.GetComponent<ChessPieceSkinController>();
        if (targetSkin != null)
        {
            targetSkin.TriggerDissolveOut(() => {
                Destroy(targetPiece.gameObject);
            });
        }
        else
        {
            // Fallback to immediate destruction
            Destroy(targetPiece.gameObject);
        }
    }
    
    /// <summary>
    /// Spawn VFX sử dụng VFXManager
    /// </summary>
    private VFXInstance SpawnVFX(VFXType vfxType, Vector3 position, Quaternion? rotation = null, float? duration = null, System.Action onComplete = null)
    {
        if (VFXManager.Instance != null)
        {
            return VFXManager.Instance.SpawnVFX(vfxType, position, rotation, duration, onComplete);
        }
        else
        {
            Debug.LogWarning("VFXManager.Instance is null! Cannot spawn VFX.");
            onComplete?.Invoke();
            return null;
        }
    }
    
    /// <summary>
    /// Despawn VFX instance cụ thể
    /// </summary>
    private void DespawnVFX(VFXInstance vfxInstance, bool immediate = false)
    {
        if (VFXManager.Instance != null && vfxInstance != null)
        {
            VFXManager.Instance.DespawnVFX(vfxInstance, immediate);
        }
    }
    
    /// <summary>
    /// Despawn tất cả VFX của piece này
    /// </summary>
    private void DespawnAllPieceVFX(bool immediate = false)
    {
        if (VFXManager.Instance != null)
        {
            // Despawn move VFX
            if (currentMoveVFX != null)
            {
                VFXManager.Instance.DespawnVFX(currentMoveVFX, immediate);
                currentMoveVFX = null;
            }
            
            // Despawn attack VFX
            if (currentAttackVFX != null)
            {
                VFXManager.Instance.DespawnVFX(currentAttackVFX, immediate);
                currentAttackVFX = null;
            }
            
            // Despawn teleport VFX
            if (currentTeleportVFX != null)
            {
                VFXManager.Instance.DespawnVFX(currentTeleportVFX, immediate);
                currentTeleportVFX = null;
            }
        }
    }
    
    /// <summary>
    /// Play sound effect
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(clip);
        }
    }
    
    /// <summary>
    /// Xử lý action tiếp theo trong queue
    /// </summary>
    private void ProcessNextQueuedAction()
    {
        if (actionQueue.Count > 0 && !IsBusy)
        {
            System.Action nextAction = actionQueue.Dequeue();
            nextAction?.Invoke();
        }
    }
    
    /// <summary>
    /// Dừng tất cả actions hiện tại
    /// </summary>
    public void StopAllActions()
    {
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = null;
        }
        
        actionQueue.Clear();
        isMoving = false;
        isAttacking = false;
        
        // Stop any DOTween animations
        transform.DOKill();
        
        // Despawn all VFX
        DespawnAllPieceVFX(true);
        
        // Reset skin state - chỉ khi GameObject còn active
        if (skinController != null && gameObject.activeInHierarchy)
        {
            skinController.SetSkinState(SkinState.Normal, false); // false = không animated
        }
    }
    
    /// <summary>
    /// Animation cast spell
    /// </summary>
    private IEnumerator CastSpellAnimation(ChessPieceInfo targetPiece, Vector3 castFromPos)
    {
        float castDuration = 0.5f;
        float elapsed = 0f;
        
        // Cast animation - có thể là glow effect, particle burst, etc.
        while (elapsed < castDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / castDuration;
            
            // Có thể thêm animation cho piece khi cast spell
            // Ví dụ: scale up/down, rotation, glow effect
            yield return null;
        }
    }
    
    /// <summary>
    /// Cast projectile đến target và đợi impact
    /// </summary>
    private IEnumerator CastProjectileToTarget(ChessPieceInfo targetPiece, Vector3 castFromPos)
    {
        Debug.Log($"CastProjectileToTarget called - Target: {targetPiece.name}, From: {castFromPos}");
        
        Vector3 targetPos = targetPiece.transform.position;
        
        // Get projectile type từ chess piece type
        ProjectileType projectileType = ProjectileInfo.GetProjectileType(pieceInfo.type);
        float projectileSpeed = ProjectileInfo.GetDefaultSpeed(projectileType);
        
        Debug.Log($"ProjectileType: {projectileType}, Speed: {projectileSpeed}");
        
        // Spawn projectile từ ProjectileManager
        GameObject projectile = null;
        if (ProjectileManager.Instance != null)
        {
            Debug.Log("ProjectileManager.Instance found, spawning projectile...");
            projectile = ProjectileManager.Instance.SpawnProjectile(projectileType, castFromPos, targetPos, projectileSpeed);
        }
        else
        {
            Debug.LogError("ProjectileManager.Instance is null!");
        }
        
        if (projectile != null)
        {
            // ProjectileController đã có sẵn từ prefab và đã được initialize
            ProjectileController controller = projectile.GetComponent<ProjectileController>();
            if (controller != null)
            {
            // Subscribe to hit event
            controller.OnHit += OnProjectileHit;
            controller.OnExplode += OnProjectileExplode;
                
                // Đợi projectile đến target hoặc hit
                float maxWaitTime = ProjectileInfo.GetDefaultLifetime(projectileType);
                float elapsed = 0f;
                
                while (elapsed < maxWaitTime && projectile != null)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
            // Unsubscribe from events
            controller.OnHit -= OnProjectileHit;
            controller.OnExplode -= OnProjectileExplode;
            }
        }
        else
        {
            // Fallback: đợi thời gian cố định
            float fallbackDuration = 2f;
            yield return new WaitForSeconds(fallbackDuration);
        }
    }
    
    /// <summary>
    /// Callback khi projectile hit target
    /// </summary>
    private void OnProjectileHit(ProjectileController projectile, GameObject target)
    {
        Debug.Log($"Projectile {projectile.Type} hit {target.name}");
        
        // Trigger dissolve và die animation cho target
        ChessPieceInfo targetPiece = target.GetComponent<ChessPieceInfo>();
        if (targetPiece != null && targetPiece.GetComponent<ChessPieceSkinController>() != null)
        {
            targetPiece.GetComponent<ChessPieceSkinController>().SetSkinStateImmediate(SkinState.Dissolving);
        }
        
        // Trigger die animation
        StartCoroutine(TriggerDieAnimation(targetPiece));
        
        // Spawn impact VFX
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnVFX(VFXType.Hit, target.transform.position);
        }
    }
    
    /// <summary>
    /// Callback khi projectile explode
    /// </summary>
    private void OnProjectileExplode(ProjectileController projectile, Vector3 explosionPos)
    {
        Debug.Log($"Projectile {projectile.Type} exploded at {explosionPos}");
        
        // Có thể thêm screen shake, camera effects, etc.
        // ScreenShake.Instance?.Shake(0.5f, 0.3f);
    }
    
    /// <summary>
    /// Trigger die animation cho target piece
    /// </summary>
    private IEnumerator TriggerDieAnimation(ChessPieceInfo targetPiece)
    {
        // Die animation - có thể là fall down, explode, etc.
        float dieDuration = 1f;
        Vector3 originalPos = targetPiece.transform.position;
        Vector3 fallPos = originalPos + Vector3.down * 0.5f;
        
        // Fall down animation
        targetPiece.transform.DOMove(fallPos, dieDuration * 0.7f).SetEase(Ease.InQuad);
        
        // Rotation animation
        targetPiece.transform.DORotate(new Vector3(0, 0, 90f), dieDuration).SetEase(Ease.InQuad);
        
        // Scale down animation
        targetPiece.transform.DOScale(Vector3.zero, dieDuration).SetEase(Ease.InQuad);
        
        yield return new WaitForSeconds(dieDuration);
    }
    
    /// <summary>
    /// Đợi target piece dissolve hoàn toàn
    /// </summary>
    private IEnumerator WaitForTargetDissolve(ChessPieceInfo targetPiece)
    {
        // Đợi dissolve effect hoàn thành
        float dissolveDuration = 2f; // Thời gian dissolve từ ChessPieceSkinController
        
        // Có thể check dissolve progress thực tế từ skin controller
        ChessPieceSkinController targetSkinController = targetPiece.GetComponent<ChessPieceSkinController>();
        if (targetSkinController != null)
        {
            // Đợi dissolve hoàn thành (có thể implement callback từ skin controller)
            yield return new WaitForSeconds(dissolveDuration);
        }
        else
        {
            // Fallback: đợi thời gian cố định
            yield return new WaitForSeconds(dissolveDuration);
        }
    }
    
    /// <summary>
    /// Thiết lập move type tùy chỉnh
    /// </summary>
    public void SetMoveType(MoveType newMoveType)
    {
        moveType = newMoveType;
    }
    
    /// <summary>
    /// Thiết lập attack type tùy chỉnh
    /// </summary>
    public void SetAttackType(AttackType newAttackType)
    {
        attackType = newAttackType;
    }
    
    private void OnDestroy()
    {
        StopAllActions();
    }
    
    // Context menu for testing
    [ContextMenu("Test Move")]
    private void TestMove()
    {
        Vector3 testTarget = transform.position + Vector3.forward * 2f;
        MovePiece(testTarget); // Sử dụng MovePiece thay vì ExecuteMove
    }
    
    [ContextMenu("Test Attack Move")]
    private void TestAttackMove()
    {
        // Find any enemy piece to attack
        ChessPieceInfo[] allPieces = FindObjectsOfType<ChessPieceInfo>();
        foreach (var piece in allPieces)
        {
            if (piece != pieceInfo && piece.isWhite != pieceInfo.isWhite)
            {
                MovePiece(piece.transform.position); // Sử dụng MovePiece
                break;
            }
        }
    }

    [ContextMenu("Test Cast Spell Attack")]
    private void TestCastSpellAttack()
    {
        SetAttackType(AttackType.CastSpellBeforeMove);
        Debug.Log("Attack type set to CastSpellBeforeMove");
    }
    
    [ContextMenu("Test Ranged Attack")]
    private void TestRangedAttack()
    {
        SetAttackType(AttackType.RangedBeforeMove);
        Debug.Log("Attack type set to RangedBeforeMove");
    }
}

// Enums for movement and attack types
[System.Serializable]
public enum MoveType
{
    Walk,       // Đi bộ thường (Pawn, King)
    Jump,       // Nhảy theo arc (Knight)
    Slide,      // Trượt nhanh (Rook)
    Teleport,   // Biến mất/xuất hiện (Bishop)
    Float       // Bay lơ lửng (Queen)
}

[System.Serializable]
public enum AttackType
{
    MeleeAfterMove,     // Di chuyển trước, đánh sau (Pawn, King, Knight)
    RangedBeforeMove,   // Bắn trước, di chuyển sau (Bishop, Rook)
    RangedThenMove,     // Bắn rồi di chuyển ngay
    MoveThenRanged,     // Di chuyển rồi bắn ngay
    CastSpellBeforeMove // Cast spell trước, đợi dissolve xong rồi mới di chuyển
}
