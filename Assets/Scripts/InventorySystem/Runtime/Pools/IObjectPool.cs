namespace DC.Data.Interfaces
{
    public interface IObjectPool<T>
    {
        T GetObject();
        void ReleaseObject(T released);
    }
}