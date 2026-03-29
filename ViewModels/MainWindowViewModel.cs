using Babel.Deck.Services;

namespace Babel.Deck.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(SessionWorkflowCoordinator coordinator)
    {
        Coordinator = coordinator;
    }

    public SessionWorkflowCoordinator Coordinator { get; }
}
