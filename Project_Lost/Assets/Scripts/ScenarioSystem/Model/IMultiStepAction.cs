namespace ScenarioSystem.Model
{
    /// <summary>
    /// 1つの ScenarioAction 内に複数のサブステップを持つことを示すインターフェース。
    /// Presenter はこのインターフェースを検知すると、次の Action に進む前にサブステップをすべて消化する。
    /// </summary>
    public interface IMultiStepAction
    {
        /// <summary>このアクションが持つサブステップの総数。</summary>
        int StepCount { get; }
    }
}
