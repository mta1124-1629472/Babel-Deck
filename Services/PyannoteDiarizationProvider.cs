public class PyannoteDiarizationProvider : BaseClass
{
    private readonly ApiKeyStore? _keyStore;
    private readonly string? _huggingFaceToken;

    public PyannoteDiarizationProvider(AppLog log, ApiKeyStore? keyStore = null, string? huggingFaceToken = null) : base(log) { _keyStore = keyStore; _huggingFaceToken = string.IsNullOrWhiteSpace(huggingFaceToken) ? null : huggingFaceToken.Trim(); }

    public async Task DiarizeAsync()
    {
        ... other code ...
        var hfToken = _keyStore?.GetKey(CredentialKeys.HuggingFace);
        ... other code ...
        var result = new
        {
            requests = new[]
            {
                [request.SourceAudioPath, minArg, maxArg],
            }
        };
    }

    public void CheckReadiness()
    {
        ... other code ...
        var store = keyStore ?? _keyStore;
        var token = store?.GetKey(CredentialKeys.HuggingFace);
        ... other code ...
    }
}