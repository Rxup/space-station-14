entity-effect-guidebook-adjust-traumas =
    { $chance ->
        [1] { $deltasign ->
                [1] Applies
                *[-1] Removes
            }
        *[other] { $deltasign ->
                [1] apply
                *[-1] remove
            }
    } {NATURALFIXED($amount, 2)} of {$traumaType} trauma

entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Suppresses
        *[other] suppress
    } pain by {NATURALFIXED($amount, 2)} for {NATURALFIXED($time, 3)} {MANY("second", $time)} (up to {NATURALFIXED($maximumSuppression, 2)} maximum suppression)

trauma-type-bone-damage = bone damage
trauma-type-organ-damage = organ damage
trauma-type-veins-damage = veins damage
trauma-type-nerve-damage = nerve damage
trauma-type-dismemberment = dismemberment
trauma-type-unknown = unknown trauma

target-body-part-head = head
target-body-part-chest = chest
target-body-part-groin = groin
target-body-part-left-arm = left arm
target-body-part-left-hand = left hand
target-body-part-right-arm = right arm
target-body-part-right-hand = right hand
target-body-part-left-leg = left leg
target-body-part-left-foot = left foot
target-body-part-right-leg = right leg
target-body-part-right-foot = right foot
target-body-part-left-full-arm = left arm and hand
target-body-part-right-full-arm = right arm and hand
target-body-part-left-full-leg = left leg and foot
target-body-part-right-full-leg = right leg and foot
target-body-part-hands = hands
target-body-part-arms = arms
target-body-part-legs = legs
target-body-part-feet = feet
target-body-part-full-arms = full arms
target-body-part-full-legs = full legs
target-body-part-body-middle = body middle (chest, groin, arms)
target-body-part-full-legs-groin = legs and groin

