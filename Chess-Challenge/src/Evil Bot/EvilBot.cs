using ChessChallenge.API;
using System;
using System.Linq;

/// <summary>
/// This chess bot was created as participation in the "Tiny chess bot challenge" by Sebastian Lague. It uses the API of the given Project that already implements all features of chess.
/// This bot implements the following features (due to the restrictions of tokens in this challenge not all features can be implemented):
/// For search:
/// - Alpha-Beta Pruning
/// - Move Ordering - after capture values, promotion
/// - Transposition Tables
/// - Quiescence Search
/// - Iterative deepening
/// For evaluation:
/// - Material difference
/// - Checkmate, Check or Draw
/// - King and pawns for endgame
/// </summary>
public class EvilBot : IChessBot
{
    //settings
    int infinity = 99999999;
    int TIME_LIMIT = 50;

    //manditory variables
    Move bestMoveThisIteration;
    Move bestMoveSoFar;
    int[] pieceValues = {0,100,320,330,500,900,20_000};
    int[] moveValues;

    
    TranspositionTableEntry?[] transpositionTable;

    byte EXACT = 0;
    byte UPPERBOUND = 1;
    byte LOWERBOUND = 2;

    public Move Think(Board board, Timer timer)
    {
        moveValues = new int[218];
        transpositionTable = new TranspositionTableEntry[10_000];

        
        IterativeDeepening(board,timer);

        return bestMoveSoFar;
    }

    void IterativeDeepening(Board board, Timer timer)
    {
        for (int i = 1; i <= 20; i++)
        {
            Search(board, i, -infinity, infinity, 0, timer);
            
            bestMoveSoFar = bestMoveThisIteration;
            bestMoveThisIteration = Move.NullMove;

            if (timer.MillisecondsElapsedThisTurn >= TIME_LIMIT)
            {
                break;
            }
        }
        
    }

    int Search(Board board, int depth, int alpha, int beta, int plyFromRoot, Timer timer)
    {
        if (timer.MillisecondsElapsedThisTurn >= TIME_LIMIT) return 0;
        if (depth == 0) return QuiescenceSearch(alpha, beta, board);

        var zobrisKey = board.ZobristKey;
        var originalAlpha = alpha;
        if (transpositionTable[zobrisKey % 10_000]?.Key == zobrisKey && transpositionTable[zobrisKey % 10_000]?.Depth >= depth)
        {
            var entry = transpositionTable[zobrisKey % 10_000];
            if (entry.Flag == EXACT) return entry.Value;
            if (entry.Flag == LOWERBOUND) alpha = Math.Max(alpha, entry.Value);
            else if (entry.Flag == UPPERBOUND) beta = Math.Min(beta, entry.Value);
        }

        if (alpha >= beta) return transpositionTable[zobrisKey % 10_000]?.Value ?? 0;

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);
        moves = OrderMoves(moves, board);

        if (moves.Length == 0 || board.IsDraw()) return board.IsInCheck() ? -9999999 : 0;

        foreach (var move in moves)
        {
            board.MakeMove(move);
            var evaluation = -Search(board, depth - 1, -beta, -alpha, plyFromRoot + 1, timer);
            board.UndoMove(move);

            if (evaluation >= beta)
            {
                transpositionTable[zobrisKey % 10_000] = new TranspositionTableEntry { Key = zobrisKey, Depth = (byte)depth, Flag = LOWERBOUND, Value = alpha };
                return beta;
            }

            if (evaluation > alpha)
            {
                alpha = evaluation;
                if (plyFromRoot == 0) bestMoveThisIteration = move;
            }
        }

        byte storeFlag = alpha <= originalAlpha ? UPPERBOUND : alpha >= beta ? LOWERBOUND : (byte)0;
        transpositionTable[zobrisKey % 10_000] = new TranspositionTableEntry { Key = zobrisKey, Depth = (byte)depth, Flag = storeFlag, Value = alpha };
        return alpha;
    }


    // Searches until the position is "quiet" (no more captures can be made)
    // In this format it uses a lot of tokens, could be integrated in the Search function.
    int QuiescenceSearch(int alpha, int beta, Board board)
    {
        int eval = Evaluate(board);
        
        if (eval >= beta)
        {
            return beta;
        }
        if (eval > alpha)
        {
            alpha = eval;
        }

        var moves = board.GetLegalMoves(true);
        foreach (var move in moves)
        {
            board.MakeMove(move);
            eval = -QuiescenceSearch(-beta, -alpha, board);
            board.UndoMove(move);
            
            if (eval >= beta)
            {
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
            }
        }

        return alpha;
    }

    /// Sorts the moves depending on how good they are, which makes alpha beta pruning a lot more efficient.
    /// This also has effects on how the bot plays. Eg. checkmate although it does not get evaluated that this is good.
    /// Downside: Consumes a lot of brain capacity. Todo: Move ordering with less tokens
    Span<Move> OrderMoves(Span<Move> moves, Board board)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            var movePieceType = board.GetPiece(move.TargetSquare).PieceType;
            var capturePieceType = board.GetPiece(move.StartSquare).PieceType;
            int score = 0;

            if (move == bestMoveSoFar) score += infinity;
            
            if (move.IsCastles) score += 50;
            if (movePieceType == PieceType.King && board.PlyCount < 15) score -= 50;

            if (move.IsCapture && capturePieceType != PieceType.King)
            {
                var delta = pieceValues[(int)movePieceType] - pieceValues[(int)capturePieceType];
                score += (board.SquareIsAttackedByOpponent(move.TargetSquare) ? (delta >= 0 ? 10000 : 2000) + delta : 10000 + delta);
            }

            if (movePieceType == PieceType.Pawn && move.IsPromotion && !move.IsCapture) score += 6000;

            moveValues[i] = score;
        }

        for (int i = 0; i < moves.Length - 1; i++)
        {
            for (int j = i + 1; j > 0; j--)
            {
                int swapIndex = j - 1;
                if (moveValues[swapIndex] < moveValues[j])
                {
                    (moves[j], moves[swapIndex]) = (moves[swapIndex], moves[j]);
                    (moveValues[j], moveValues[swapIndex]) = (moveValues[swapIndex], moveValues[j]);
                }
            }
        }

        return moves;
    }


    int Evaluate(Board board)
    {
        
        var perspective = board.IsWhiteToMove ? 1 : -1;
        int whiteValue = 0, blackValue = 0,value = 0;
        
        var ply = board.PlyCount;
        var isEngame = ply > 20;
        
        if (board.IsInCheckmate())
        {
            return infinity;
        }
        if (board.IsDraw())
        {
            return 0;
        }
        PieceList[] pieceLists = board.GetAllPieceLists();
        Piece blackKing = pieceLists[11][0], whiteKing = pieceLists[5][0];
        
        for (int i = 0; i < pieceLists.Length; i++)
        {
            foreach (var piece in pieceLists[i])
            {
                var rank = piece.Square.Rank;
                switch (i)
                {
                    case 0:
                        //white pawn
                        whiteValue += 100;
                        //pawns should promote in endgame
                        whiteValue += isEngame ? rank : 0;
                        break;
                    case 1:
                        //white knight
                        whiteValue += 320;
                        whiteValue += rank == 0 ? -10 :0;
                        break;
                    case 2:
                        //white bishop
                        whiteValue += 330;
                        whiteValue += rank == 0 ? -10 :0;
                        break;
                    case 3:
                        //white rook
                        whiteValue += 500;
                        break;
                    case 4:
                        //white queen
                        whiteValue += 1100;
                        break;
                    case 6:
                        //black pawn
                        blackValue += 100;
                        blackValue += isEngame ? 7 - rank : 0;
                        break;
                    case 7:
                        //black knight
                        blackValue += 320;
                        blackValue += rank == 7 ? -10 :0;
                        break;
                    case 8:
                        //black bishop
                        blackValue += 330;
                        blackValue += rank == 7 ? -10 :0;
                        break;
                    case 9:
                        //black rook
                        blackValue += 500;
                        break;
                    case 10:
                        //black queen
                        blackValue += 1100;
                        break;
                }
            }
        }
        
        //in the engame the kings should move towards each other to 
        var distance = Math.Abs(whiteKing.Square.File - blackKing.Square.File) +
                       Math.Abs(whiteKing.Square.Rank - blackKing.Square.Rank);
        
        value += isEngame ? (14 - distance)*5:0;
        return perspective * (whiteValue - blackValue) + value;
    }
}
