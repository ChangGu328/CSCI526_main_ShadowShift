using UnityEngine;
public class PlateConnector : MonoBehaviour
{
    [Header("ExclusivePlate")]
    public ExclusivePlate plate;
    [Header("button")]
    public ExclusivePlate pairedPlate;

    [Header("bobo")]
    public ParticleSystem selfParticle;
    public ParticleSystem pairedParticle;

    void Update()
    {
        bool bothPressed = plate.IsPressed && pairedPlate.IsPressed;
        bool anyPressed = (plate.IsPressed || pairedPlate.IsPressed) && !bothPressed;

        if (selfParticle)
        {
            if (anyPressed && !selfParticle.isPlaying) selfParticle.Play();
            else if (!anyPressed && selfParticle.isPlaying) selfParticle.Stop();
        }
        if (pairedParticle)
        {
            if (anyPressed && !pairedParticle.isPlaying) pairedParticle.Play();
            else if (!anyPressed && pairedParticle.isPlaying) pairedParticle.Stop();
        }
    }
}