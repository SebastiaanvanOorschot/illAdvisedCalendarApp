import { watch, Ref } from 'vue';

/**
 * Composable to handle browser back button for modals
 * When modal is open, back button closes the modal instead of leaving the app
 */
export function useBackButton(isOpen: Ref<boolean>, onClose: () => void) {
    function handleBackButton(event: PopStateEvent) {
        if (isOpen.value) {
            event.preventDefault();
            onClose();
        }
    }

    watch(isOpen, (newValue) => {
        if (newValue) {
            // Modal opened - push history state and add listener
            window.history.pushState({ modal: true }, '');
            window.addEventListener('popstate', handleBackButton);
        } else {
            // Modal closed - remove listener
            window.removeEventListener('popstate', handleBackButton);
        }
    });

    // Cleanup on component unmount
    return () => {
        window.removeEventListener('popstate', handleBackButton);
    };
}
