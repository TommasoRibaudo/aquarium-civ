// #96: the phone's own taptic feel for Aquarium Civ's buzzes (AquariumCiv.View.HapticOut). `kind` is
// HapticPattern.IosKind: 1 Selection, 2 ImpactLight, 3 ImpactMedium, 4 ImpactHeavy, 5 ImpactSoft, 6 NotifySuccess,
// 7 NotifyWarning. Does nothing on anything else.
#import <UIKit/UIKit.h>

extern "C" void AqHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind)
        {
            case 1: { UISelectionFeedbackGenerator *g = [[UISelectionFeedbackGenerator alloc] init]; [g selectionChanged]; break; }
            case 2: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [g impactOccurred]; break; }
            case 3: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium]; [g impactOccurred]; break; }
            case 4: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy]; [g impactOccurred]; break; }
            case 5:
            {
                if (@available(iOS 13.0, *)) { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft]; [g impactOccurred]; }
                else { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [g impactOccurred]; }
                break;
            }
            case 6: { UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init]; [g notificationOccurred:UINotificationFeedbackTypeSuccess]; break; }
            case 7: { UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init]; [g notificationOccurred:UINotificationFeedbackTypeWarning]; break; }
            default: break;
        }
    });
}
