using UnityEngine;
namespace CampaignDemo
{
    // The UI owns selection/mode decisions. The command service does not depend on UI or current camera player.
    public sealed class BattleUIActions : MonoBehaviour
    {
        public BattleCommandService commands;
        public CommandResult Execute(BattleCommand command)
        {
            if(commands==null||commands.world==null||command==null)return CommandResult.No("命令入口未绑定");
            var world=commands.world;if(world.setup.modes.CurrentMode!=MouseCommandMode.Interaction)return CommandResult.No("请先按 V");
            command.playerId=world.currentPlayer;return commands.Submit(command);
        }
    }
}
