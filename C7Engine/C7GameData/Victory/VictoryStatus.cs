namespace C7GameData;

/// Shared status class for easy abstractions
public class VictoryStatus {
	public Player Player { get; set; }
	public int CurrentTurn { get; set; }
	public float TurnScore { get; set; }
	public int RivalsAlive { get; set; }
	public float DominationArea { get; set; }
	public float DominationPopulation { get; set; }
	public float Score { get; set; }
	public int HighestCityCulture { get; set; }
	public long EmpireCulture { get; set; }
	public long RivalEmpireCulture { get; set; }
}
