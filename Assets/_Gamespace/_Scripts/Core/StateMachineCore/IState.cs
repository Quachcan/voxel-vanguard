namespace _Workspace._Scripts.Core.StateMachineCore
{
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick();
        void FixedTick();
    }
}