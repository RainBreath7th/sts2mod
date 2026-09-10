namespace HextechRunes;

/// <summary>生成式符文的实例数据；同一模型的不同实例可以拥有不同配方。</summary>
public interface IHextechGeneratedRune
{
	string ExportSelectionData();
	bool TryImportSelectionData(string data);
}
