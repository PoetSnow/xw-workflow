

namespace XW.Infra.Core.DependencyInjection
{

    /// <summary>
    /// 提供对根作用域 IServiceProvider 实例的访问
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _provider;

        static ServiceLocator()
        {
        }

        /// <summary>
        /// 设置 IServiceProvider 实例
        /// </summary>
        /// <param name="provider"></param>
        /// <exception cref="InvalidOperationException">如果 Provider 已被设置，则抛出此异常。</exception>
        /// <exception cref="ArgumentNullException">如果设置 Provider 时传入的值为 null，则抛出此异常。</exception>
        public static void SetProvider(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(provider));

            if (_provider is not null)
            {
                throw new InvalidOperationException($"{nameof(_provider)} 已被设置。");
            }
            _provider = provider;
        }

        /// <summary>
        /// 获取 IServiceProvider 实例
        /// </summary>
        /// <returns>IServiceProvider 实例</returns>
        /// <exception cref="InvalidOperationException">如果 Provider 未被设置，则抛出此异常。</exception>
        public static IServiceProvider GetProvider()
        {
            if (_provider is null)
            {
                throw new InvalidOperationException($"{nameof(_provider)} 未被设置。");
            }

            return _provider;
        }

        /// <summary>
        /// 检查 IServiceProvider 实例是否已被设置
        /// </summary>
        /// <returns>如果 IServiceProvider 实例已被设置，则为 true；否则为 false。</returns>
        public static bool HasProvider() => _provider is not null;
    }

}

