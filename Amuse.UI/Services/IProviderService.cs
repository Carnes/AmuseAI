using Amuse.UI.Enums;
using OnnxStack.Core.Model;
using OnnxStack.StableDiffusion.Enums;

namespace Amuse.UI.Services
{
    public interface IProviderService
    {
        OnnxExecutionProvider GetProvider(ExecutionProvider? provider, int? deviceId);
    }
}
