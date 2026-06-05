<script setup lang="ts">
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import PageHeader from '@/components/common/PageHeader.vue'
import OverviewTab from './OverviewTab.vue'
import InboundTab from './InboundTab.vue'
import OutboundTab from './OutboundTab.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1600px">
    <PageHeader
      title="Dashboard"
      :subtitle="auth.user ? `Welcome back, ${auth.user.firstName}.` : undefined"
    />

    <Tabs value="overview">
      <TabList>
        <Tab value="overview">Overview</Tab>
        <Tab value="inbound">Inbound</Tab>
        <Tab value="outbound">Outbound</Tab>
        <!-- Future tabs slot in here: Inventory, Capacity -->
      </TabList>
      <TabPanels class="!px-0">
        <TabPanel value="overview">
          <OverviewTab />
        </TabPanel>
        <TabPanel value="inbound">
          <InboundTab />
        </TabPanel>
        <TabPanel value="outbound">
          <OutboundTab />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </section>
</template>
