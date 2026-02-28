import { AppLayout } from '@/components/app-layout';
import { ChatSkeleton } from '@/components/skeletons';

export default function ChatLoading() {
  return (
    <AppLayout mainClassName="relative z-10 flex-1 flex flex-col max-w-4xl mx-auto w-full pb-24 md:pb-0" noBackground>
      <ChatSkeleton />
    </AppLayout>
  );
}
