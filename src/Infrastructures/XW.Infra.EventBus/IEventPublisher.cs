namespace XW.Infra.EventBus;

public interface IEventPublisher
{
    /// <summary>
    /// �첽����������Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishAsync<T>(T contentObj, string? callbackName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// �첽�������Զ����ͷ�Ķ�����Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishAsync<T>(T contentObj, IDictionary<string, string?> headers, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// ����������Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    void Publish<T>(T contentObj, string? callbackName = null) where T : class;

    /// <summary>
    /// �������Զ����ͷ�Ķ�����Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    void Publish<T>(T contentObj, IDictionary<string, string?> headers) where T : class;

    /// <summary>
    /// �첽����һ����Ϣ���ڽ���ָ��ʱ��󷢲�������ͷ����
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishDelayAsync<T>(TimeSpan delayTime, T? contentObj, IDictionary<string, string?> headers, CancellationToken cancellationToken = default);

    /// <summary>
    /// �첽����һ����Ϣ���ڽ���ָ��ʱ��󷢲���
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishDelayAsync<T>(TimeSpan delayTime, T? contentObj, string? callbackName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// ����һ����Ϣ���ڽ���ָ��ʱ��󷢲�������ͷ����
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    void PublishDelay<T>(TimeSpan delayTime, T? contentObj, IDictionary<string, string?> headers);

    /// <summary>
    /// ����һ����Ϣ���ڽ���ָ��ʱ��󷢲���
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    void PublishDelay<T>(TimeSpan delayTime, T? contentObj, string? callbackName = null);

    /// <summary>
    /// �첽����������Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishAsync<T>(string name, T? contentObj, string? callbackName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// �첽�������Զ����ͷ�Ķ�����Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishAsync<T>(string name, T? contentObj, IDictionary<string, string?> headers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ����������Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    void Publish<T>(string name, T? contentObj, string? callbackName = null);

    /// <summary>
    /// �������Զ����ͷ�Ķ�����Ϣ��
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    void Publish<T>(string name, T? contentObj, IDictionary<string, string?> headers);

    /// <summary>
    /// �첽����һ����Ϣ���ڽ���ָ��ʱ��󷢲�������ͷ����
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishDelayAsync<T>(TimeSpan delayTime, string name, T? contentObj, IDictionary<string, string?> headers, CancellationToken cancellationToken = default);

    /// <summary>
    /// �첽����һ����Ϣ���ڽ���ָ��ʱ��󷢲���
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    /// <param name="cancellationToken">ȡ�����ơ�</param>
    /// <returns>��ʾ�첽����������</returns>
    Task PublishDelayAsync<T>(TimeSpan delayTime, string name, T? contentObj, string? callbackName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// ����һ����Ϣ���ڽ���ָ��ʱ��󷢲�������ͷ����
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="headers">��Ϣ���ӱ�ͷ��</param>
    void PublishDelay<T>(TimeSpan delayTime, string name, T? contentObj, IDictionary<string, string?> headers);

    /// <summary>
    /// ����һ����Ϣ���ڽ���ָ��ʱ��󷢲���
    /// </summary>
    /// <typeparam name="T">��Ϣ���ݶ�������͡�</typeparam>
    /// <param name="delayTime">��Ϣ�������ӳ�ʱ�䡣</param>
    /// <param name="name">�������ƻ򽻻���·�ɼ���</param>
    /// <param name="contentObj">�������л�����Ϣ�����ݡ�����Ϊ null��</param>
    /// <param name="callbackName">�ص����������ơ�</param>
    void PublishDelay<T>(TimeSpan delayTime, string name, T? contentObj, string? callbackName = null);
}
