using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;

namespace WhoWhoo3D.Gameplay.Game;

public interface IMatchConnection
{
    bool CanPlay(PlayerId active);
    bool Submit(ClaimSlotCommand command);
}
