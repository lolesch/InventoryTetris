namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// One synchronous step of resource regeneration, carved out of <see cref="BaseCharacter"/>'s
    /// old <c>async void</c> <c>Update()</c> path (issue #17) so the Encounter sim can later drive
    /// regen from the combat tick instead of the frame loop.
    /// <para>
    /// <paramref name="recoveryDelay"/> reproduces the old per-resource post-depletion rule:
    /// a negative value never regenerates while the resource is empty (Health &#8212; the character
    /// is dead), zero regenerates immediately (Resource), and a positive value waits that many
    /// seconds after the resource empties before regen resumes (Shield). <paramref name="secondsEmpty"/>
    /// is the caller-held accumulator tracking progress through that wait &#8212; feed each call's
    /// return value back in as the next step's <paramref name="secondsEmpty"/>.
    /// </para>
    /// </summary>
    public static class ResourceRegen
    {
        /// <returns>The updated "seconds this resource has been empty" accumulator.</returns>
        public static float Step(
            CharacterResource resource,
            float regenPerSecond,
            float recoveryDelay,
            float secondsEmpty,
            float deltaSeconds)
        {
            if (resource.IsDepleted)
            {
                if (recoveryDelay < 0f)
                    return secondsEmpty; // never recovers from empty (the Health sentinel)

                secondsEmpty += deltaSeconds;

                if (secondsEmpty < recoveryDelay)
                    return secondsEmpty; // still inside the post-depletion wait
            }
            else
                secondsEmpty = 0f; // topped back up - reset the wait

            _ = resource.AddToCurrent(regenPerSecond * deltaSeconds);

            return secondsEmpty;
        }
    }
}
