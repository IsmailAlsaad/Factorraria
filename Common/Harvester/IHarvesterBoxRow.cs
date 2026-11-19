namespace Factorraria.Common.Harvester
{
    // What the network side needs from a Harvester Box row entity (the entity class is generic, so it cannot be named directly).
    public interface IHarvesterBoxRow
    {
        // Client: the server started the strike of the box at world tile x. Plays the staff animation (the server already runs the real cycle).
        void BeginClientStrike(int tileX);
    }
}