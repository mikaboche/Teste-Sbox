using System.Net;
using System.Threading.Tasks;
using Sandbox;

namespace SboxPro;

// [SkipHotload] on the type itself: HotloadManager won't walk INTO McpSession looking
// for lambdas to substitute. Belt-and-suspenders alongside the [SkipHotload] on the
// _sessions dictionary that holds these — covers the case where someone else (a logger,
// a debug dump, anything) holds a transient ref to one of these.
[SkipHotload]
public sealed class McpSession
{
	public string SessionId { get; init; }
	public HttpListenerResponse SseResponse { get; init; }
	public TaskCompletionSource<bool> Tcs { get; } = new();
	public bool Initialized { get; set; }
}
