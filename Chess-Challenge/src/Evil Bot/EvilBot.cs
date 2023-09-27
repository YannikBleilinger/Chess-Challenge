using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

//todo: iterative deepening
//todo: king safety and endgame ?

/// <summary>
/// This chess bot was created as participation in the "Tiny chess bot challenge" by Sebastian Lague. It uses the API of the given Project that already implements all features of chess.
/// This bot implements the following features (due to the restrictions of tokens in this challenge not all features can be implemented):
/// For search:
/// - Alpha-Beta Pruning
/// - Move Ordering - after capture values, promotion
/// - Transposition Tables
/// - Todo: Quiescence Search
/// - Todo: Iterative deepening
/// For evaluation:
/// - Material difference
/// - Checkmate, Check or Draw
/// - Todo: King safety
/// - Todo: King for endgame
/// </summary>
public class EvilBot : IChessBot
{
    //settings
    private const int MAX_DEPTH = 5;
    private const int infinity = 99999999;
    private const int timeLimit = 999999;
    private const int TranspositionTableEntries = 100000;

    //manditory variables
    Move bestMoveThisPosition = Move.NullMove;
    Move bestMoveThisIteration = Move.NullMove;
    private Move lastBestMove = Move.NullMove;
    int[] pieceValues = {0,100,320,330,500,900,20000};
    
    const byte EXACT = 0;
    const byte UPPERBOUND = 1;
    const byte LOWERBOUND = 2;

    private int[] moveValues;

    private TranspositionTableEntry?[] TranspositionTable;
    
    public Move Think(Board board, Timer timer)
    {
        moveValues = new int[218];
        TranspositionTable = new TranspositionTableEntry[TranspositionTableEntries];
        
        Search(board, MAX_DEPTH, -infinity, infinity,0,timer);
        
        return bestMoveThisIteration;
    }
    
    private int Search(Board board, int depth, int alpha, int beta, int plyFromRoot, Timer timer)
    {
        //looks in transposition table if the position was evaluated before, currently only gives back the value of the position. Todo: Return of Move and NodeType has to be implemented
        var zobristKey = board.ZobristKey;
        var originalAlpha = alpha;
        TranspositionTableEntry? entry = TranspositionTable[zobristKey % (ulong)TranspositionTableEntries];
        if (entry != null && entry.Key == zobristKey && entry.Depth >= depth)
        {
            byte flag = entry.Flag;
            if (flag == EXACT) return entry.Value;
            if (flag == LOWERBOUND)
            {
                alpha = Math.Max(alpha, entry.Value);
            }else if (flag == UPPERBOUND) beta = Math.Min(beta, entry.Value);

        }

        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
        {
            return 0;
        }
        //at he max depth starts evaluating board position, todo: quisence serach - searches till no captures can be made anymore
        if (depth == 0) return Evaluate(board);
        
        Move[] moves = OrderMoves(board.GetLegalMoves(),board);
        byte bound = 1; //upper bound
        
        if (moves.Length == 0 || board.IsDraw())
        {
            if (board.IsInCheck()) return -infinity;

            return 0;
        }

        //loops trough all legal moves and plays each of them, recursively calls itself to get each response of the enemy, prunes if best move for opposite site has been found
        foreach (var move in moves)
        {
            board.MakeMove(move);
            var evaluation = -Search(board,depth - 1, -beta, -alpha,plyFromRoot+1,timer);
            board.UndoMove(move);
            
            //move is to good for the opposite side and gets pruned
            if (evaluation >= beta)
            {
                return beta; //SNIPP
            }
            
            if(evaluation>alpha)
            {
                bestMoveThisPosition = move;
                alpha = evaluation;
                bound = 0; //Exact = 0
                if (plyFromRoot == 0)
                {
                    bestMoveThisIteration = move;
                }
            }
        }
        
        byte storeFlag = 0;
        if (alpha <= originalAlpha) storeFlag = UPPERBOUND;
        if (alpha >= beta) storeFlag = LOWERBOUND;

        TranspositionTable[zobristKey % (ulong)TranspositionTableEntries] = new TranspositionTableEntry {Key = zobristKey, Depth = (byte)depth,Flag = storeFlag,Value = alpha};

        return alpha;
    }

    private Move[] OrderMoves(Move[] moves, Board board)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            
            var move = moves[i];
            var movePieceType = board.GetPiece(move.TargetSquare).PieceType;
            var capturePieceType = board.GetPiece(move.StartSquare).PieceType;
            int score = 0;
            
            //checks if move is a capture, the won material difference and if the piece can be captured from the target sqare
            if (move.IsCapture)
            {
                var delta = pieceValues[(int)movePieceType] 
                                        - pieceValues[(int)capturePieceType];
                if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    score += (delta >= 0 ? 8000 : 2000) + delta;
                }
                else score += 8000 + delta;
            }

            //checks if piece can be promoted and not be captured
            if (movePieceType == PieceType.Pawn && move.IsPromotion && !move.IsCapture)
            {
                score += 6000;
            }

            moveValues[i] = score;
        }
        
        //sort the array
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
        int perspective = board.IsWhiteToMove ? 1 : -1;
        var value = 0;
        if (board.IsInCheckmate())
        {
            return infinity;
        }
        if (board.IsDraw())
        {
            return 0;
        }

        //value += board.GetLegalMoves().Length * 5;
        value += board.GetKingSquare(board.IsWhiteToMove).File == (6|2)? 20: 0;
        value += getMaterialDifference(board);
        
        
        return value*perspective;
    }

    
    int getMaterialDifference(Board board)
    {
        var sum = 0;
        PieceList[] pieceLists = board.GetAllPieceLists();
        for (int i = 0; i < pieceLists.Length-1; i++)
        {
            if (i == 5) continue;
            
            sum += (i < 6) ? pieceLists[i].Count * pieceValues[i+1] : -pieceLists[i].Count * pieceValues[i-5];
        }

        return sum;
    }

}